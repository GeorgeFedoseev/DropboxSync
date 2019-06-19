using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class TransferManager : IDisposable {

        private DropboxSyncConfiguration _config;

        // queued
        private Queue<DownloadFileTransfer> _downloadTransferQueue = new Queue<DownloadFileTransfer> ();
        public int CurrentQueuedDownloadTransfersNumber => _downloadTransferQueue.Count;

        private Queue<UploadFileTransfer> _uploadTransferQueue = new Queue<UploadFileTransfer> ();
        public int CurrentQueuedUploadTransfersNumber => _uploadTransferQueue.Count;

        // current
        private List<IFileTransfer> _currentDownloadTransfers = new List<IFileTransfer> ();
        public int CurrentDownloadTransferNumber => _currentDownloadTransfers.Count;

        private List<IFileTransfer> _currentUploadTransfers = new List<IFileTransfer> ();
        public int CurrentUploadTransferNumber => _currentUploadTransfers.Count;

        // failed
        private List<IFileTransfer> _failedTransfers = new List<IFileTransfer> ();
        public int FailedTransfersNumber => _failedTransfers.Count;

        // completed
        private List<IFileTransfer> _completedTransfers = new List<IFileTransfer> ();
        public int CompletedTransferNumber => _completedTransfers.Count;

        private object _transfersLock = new object ();

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        public TransferManager (DropboxSyncConfiguration config) {
            _config = config;

            // clean up temp files from prev launch
            DeleteAllTempDownloadFiles();

            _backgroundThread = new Thread (_backgroudWorker);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start ();
        }

        public void MaybeAddFromQueue () {
            lock (_transfersLock) {
                // download
                if (_currentDownloadTransfers.Count < _config.maxSimultaneousDownloadFileTransfers) {
                    // can add more
                    int canAddNum = _config.maxSimultaneousDownloadFileTransfers - _currentDownloadTransfers.Count;
                    int addNum = Math.Min (canAddNum, _downloadTransferQueue.Count);

                    if (addNum > 0) {
                        Debug.Log ($"[TransferManager] Adding {addNum} download transfers to process");
                        for (var i = 0; i < addNum; i++) {
                            var transfer = _downloadTransferQueue.Dequeue ();
                            // fire and forget
                            ProcessTransferAsync (transfer);
                            _currentDownloadTransfers.Add (transfer);
                        }
                    }
                }

                // upload
                if (_currentUploadTransfers.Count < _config.maxSimultaneousUploadFileTransfers) {
                    // can add more
                    int canAddNum = _config.maxSimultaneousUploadFileTransfers - _currentUploadTransfers.Count;
                    int addNum = Math.Min (canAddNum, _uploadTransferQueue.Count);

                    if (addNum > 0) {
                        Debug.Log ($"[TransferManager] Adding {addNum} upload transfers to process");
                        for (var i = 0; i < addNum; i++) {
                            var transfer = _uploadTransferQueue.Dequeue ();
                            // fire and forget
                            ProcessTransferAsync (transfer);
                            _currentUploadTransfers.Add (transfer);
                        }
                    }
                }
            }
        }

        private void _backgroudWorker () {
            while (!_isDisposed) {
                MaybeAddFromQueue ();
                Thread.Sleep (100);
            }
        }

        // METHODS
        public async Task<Metadata> DownloadFileAsync (string dropboxPath, string localPath, IProgress<TransferProgressReport> progressCallback) {
            var completionSource = new TaskCompletionSource<Metadata> ();
            var downloadTransfer = new DownloadFileTransfer (dropboxPath, localPath, progressCallback, completionSource, _config);
            return await _DownloadFileAsync (downloadTransfer);
        }

        public async Task<Metadata> DownloadFileAsync (Metadata metadata, string localPath, IProgress<TransferProgressReport> progressCallback) {
            var completionSource = new TaskCompletionSource<Metadata> ();
            var downloadTransfer = new DownloadFileTransfer (metadata, localPath, progressCallback, completionSource, _config);
            return await _DownloadFileAsync (downloadTransfer);
        }

        public async Task<Metadata> UploadFileAsync(string localPath, string dropboxPath, IProgress<TransferProgressReport> progressCallback) {
            var completionSource = new TaskCompletionSource<Metadata> ();
            var uploadTransfer = new UploadFileTransfer (localPath, dropboxPath, progressCallback, completionSource, _config);
            return await _UploadFileAsync (uploadTransfer);
        }

        private async Task<Metadata> _DownloadFileAsync (DownloadFileTransfer transfer) {
            // check if transfer is already queued or in process
            // if so, subscribe to its completion
            var alreadyHave = GetQueuedOrExecutingDownloadTransfer (transfer.DropboxPath, transfer.LocalPath);
            if (alreadyHave != null) {
                return await alreadyHave.CompletionSource.Task;
            }
            // otherwise put new transfer to queue
            lock (_transfersLock) {
                _downloadTransferQueue.Enqueue (transfer);
            }
            // and subscribe to completion
            return await transfer.CompletionSource.Task;
        }

        private async Task<Metadata> _UploadFileAsync (UploadFileTransfer transfer) {
            // check if transfer is already queued or in process
            // if so, subscribe to its completion
            var alreadyHave = GetQueuedOrExecutingUploadTransfer (transfer.DropboxPath, transfer.LocalPath);
            if (alreadyHave != null) {
                return await alreadyHave.CompletionSource.Task;
            }
            // otherwise put new transfer to queue
            lock (_transfersLock) {
                _uploadTransferQueue.Enqueue (transfer);
            }
            // and subscribe to completion
            return await transfer.CompletionSource.Task;
        }

        private DownloadFileTransfer GetQueuedOrExecutingDownloadTransfer (string dropboxPath, string localPath) {
            lock (_transfersLock) {
                var executing = _currentDownloadTransfers.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) 
                                && t.LocalPath == localPath);
                if (executing != null) {
                    return executing as DownloadFileTransfer;
                }

                var queued = _downloadTransferQueue.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) && t.LocalPath == localPath);
                if (queued != null) {
                    return queued;
                }
            }

            return null;
        }

        private UploadFileTransfer GetQueuedOrExecutingUploadTransfer (string dropboxPath, string localPath) {
            lock (_transfersLock) {
                var executing = _currentUploadTransfers.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) 
                                    && t.LocalPath == localPath);
                if (executing != null) {
                    return executing as UploadFileTransfer;
                }

                var queued = _uploadTransferQueue.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) && t.LocalPath == localPath);
                if (queued != null) {
                    return queued;
                }
            }

            return null;
        }

        private async void ProcessTransferAsync (IFileTransfer transfer) {

            try {
                var metadata = await transfer.ExecuteAsync ();
                transfer.CompletionSource.SetResult (metadata);

                // move to completed
                lock (_transfersLock) {
                    if(transfer is DownloadFileTransfer){
                        _currentDownloadTransfers.Remove (transfer);
                    }else if(transfer is UploadFileTransfer){
                        _currentUploadTransfers.Remove (transfer);
                    }                    
                    _completedTransfers.Add (transfer);

                    Debug.Log ($"[TransferManager] Transfer completed, moving to completed (now {_completedTransfers.Count} completed)");
                }
            } catch (Exception ex) {

                transfer.CompletionSource.SetException (ex);

                // move to failed
                lock (_transfersLock) {
                    if(transfer is DownloadFileTransfer){
                        _currentDownloadTransfers.Remove (transfer);
                    }else if(transfer is UploadFileTransfer){
                        _currentUploadTransfers.Remove (transfer);
                    }

                    _failedTransfers.Add (transfer);

                    Debug.Log ($"[TransferManager] Transfer failed, moving to failed (now {_failedTransfers.Count} failed)");
                }
            }
        }

        private void DeleteAllTempDownloadFiles () {
            if(Directory.Exists(_config.cacheDirecoryPath)){
                foreach (string file in Directory.GetFiles (_config.cacheDirecoryPath, $"*{DropboxSyncConfiguration.INTERMEDIATE_DOWNLOAD_FILE_EXTENSION}", SearchOption.AllDirectories)) {
                    File.Delete (file);
                }
            }            
        }

        public void Dispose () {
            // stop adding new transfers
            _isDisposed = true;
            // cancel current tranfers
            lock (_transfersLock) {
                _currentDownloadTransfers.ForEach (x => x.Cancel ());
                _currentUploadTransfers.ForEach (x => x.Cancel ());
            }
        }
    }

}