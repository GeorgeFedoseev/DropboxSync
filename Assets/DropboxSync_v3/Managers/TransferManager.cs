using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class TransferManager : IDisposable {

        public Action OnTransfersListChanged = () => {};

        private DropboxSyncConfiguration _config;

        // queued
        private List<DownloadFileTransfer> _downloadTransferQueue = new List<DownloadFileTransfer> ();        
        public int CurrentQueuedDownloadTransfersNumber => _downloadTransferQueue.Count;

        private List<UploadFileTransfer> _uploadTransferQueue = new List<UploadFileTransfer> ();
        public int CurrentQueuedUploadTransfersNumber => _uploadTransferQueue.Count;

        public List<IFileTransfer> QueuedTransfers => _downloadTransferQueue.Select(x => x as IFileTransfer).Concat(_uploadTransferQueue).ToList();

        // current
        private List<IFileTransfer> _currentDownloadTransfers = new List<IFileTransfer> ();
        public int CurrentDownloadTransferNumber => _currentDownloadTransfers.Count;

        private List<IFileTransfer> _currentUploadTransfers = new List<IFileTransfer> ();
        public int CurrentUploadTransferNumber => _currentUploadTransfers.Count;

        public List<IFileTransfer> ActiveTransfers => _currentDownloadTransfers.Concat(_currentUploadTransfers).ToList();

        // speed
        public double CurrentTotalDownloadSpeedBytesPerSecond => _currentDownloadTransfers.Select(t => t.Progress.bytesPerSecondSpeed).Sum();
        public string CurrentTotalDownloadSpeedFormatted => TransferSpeedTracker.FormatBytesPerSecond(CurrentTotalDownloadSpeedBytesPerSecond);

        public double CurrentTotalUploadSpeedBytesPerSecond => _currentUploadTransfers.Select(t => t.Progress.bytesPerSecondSpeed).Sum();
        public string CurrentTotalUploadSpeedFormatted => TransferSpeedTracker.FormatBytesPerSecond(CurrentTotalUploadSpeedBytesPerSecond);

        // failed
        private List<IFileTransfer> _failedTransfers = new List<IFileTransfer> ();
        public int FailedTransfersNumber => _failedTransfers.Count;

        public List<IFileTransfer> FailedTransfers => _failedTransfers;

        // completed
        private List<IFileTransfer> _completedTransfers = new List<IFileTransfer> ();
        public int CompletedTransferNumber => _completedTransfers.Count;

        public List<IFileTransfer> CompletedTransfers => _completedTransfers;

        // private object _transfersLock = new object ();
        
        private volatile bool _isDisposed = false;

        public TransferManager (DropboxSyncConfiguration config) {
            _config = config;

            // clean up temp files from prev launch
            DeleteAllTempDownloadFiles();
            
            _queueSpinner();
        }

        public void MaybeAddFromQueue () {
            bool addedAnything = false;
            // lock (_transfersLock) {
                // download
                if (_currentDownloadTransfers.Count < _config.maxSimultaneousDownloadFileTransfers) {
                    // can add more
                    int canAddNum = _config.maxSimultaneousDownloadFileTransfers - _currentDownloadTransfers.Count;
                    int addNum = Math.Min (canAddNum, _downloadTransferQueue.Count);

                    if (addNum > 0) {
                        Debug.Log ($"[DropboxSync/TransferManager] Adding {addNum} download transfers to process");
                        for (var i = 0; i < addNum; i++) {
                            var transfer = _downloadTransferQueue.First(); _downloadTransferQueue.RemoveAt (0);
                            // fire and forget
                            ProcessTransferAsync (transfer);
                            _currentDownloadTransfers.Add (transfer);
                            addedAnything = true;
                        }
                    }
                }

                // upload
                if (_currentUploadTransfers.Count < _config.maxSimultaneousUploadFileTransfers) {
                    // can add more
                    int canAddNum = _config.maxSimultaneousUploadFileTransfers - _currentUploadTransfers.Count;
                    int addNum = Math.Min (canAddNum, _uploadTransferQueue.Count);

                    if (addNum > 0) {
                        Debug.Log ($"[DropboxSync/TransferManager] Adding {addNum} upload transfers to process");
                        for (var i = 0; i < addNum; i++) {
                            var transfer = _uploadTransferQueue.First(); _uploadTransferQueue.RemoveAt (0);
                            // fire and forget
                            ProcessTransferAsync (transfer);
                            _currentUploadTransfers.Add (transfer);
                            addedAnything = true;
                        }
                    }
                }
            // }        

            if(addedAnything){
                OnTransfersListChanged();
            }    
        }

        private void ClearQueueFromCancelledTransfers(){
            bool removedAnything = false;
            // lock (_transfersLock) {
                // downloads
                foreach(var tr in _downloadTransferQueue.ToList()){
                    if(tr.CancellationToken.IsCancellationRequested){
                        // Debug.Log($"ClearQueueFromCancelledTransfers: {tr}");
                        tr.CompletionSource.SetException(new OperationCanceledException(tr.CancellationToken));   
                        _downloadTransferQueue.Remove(tr);     
                        removedAnything = true;                
                    }
                }

                // uploads
                foreach(var tr in _uploadTransferQueue.ToList()){
                    if(tr.CancellationToken.IsCancellationRequested){
                        // Debug.Log($"ClearQueueFromCancelledTransfers: {tr}");
                        tr.CompletionSource.SetException(new OperationCanceledException(tr.CancellationToken));   
                        _uploadTransferQueue.Remove(tr);
                        removedAnything = true;
                    }
                }                
            // }

            if(removedAnything){
                OnTransfersListChanged();
            }            
        }

        private async void _queueSpinner () {
            while (!_isDisposed) {
                ClearQueueFromCancelledTransfers();
                MaybeAddFromQueue ();
                await Task.Delay (100);
            }
        }

        // METHODS
        public async Task<Metadata> DownloadFileAsync (string dropboxPath, string localPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken = null) {            
            var downloadTransfer = new DownloadFileTransfer (dropboxPath, localPath, progressCallback, _config, cancellationToken);
            return await _DownloadFileAsync (downloadTransfer);
        }

        public async Task<Metadata> DownloadFileAsync (Metadata metadata, string localPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken = null) {            
            var downloadTransfer = new DownloadFileTransfer (metadata, localPath, progressCallback, _config, cancellationToken);
            return await _DownloadFileAsync (downloadTransfer);
        }

        public async Task<Metadata> UploadFileAsync(string localPath, string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken = null) {            
            var uploadTransfer = new UploadFileTransfer (localPath, dropboxPath, progressCallback, _config, cancellationToken);
            return await _UploadFileAsync (uploadTransfer);
        }

        private async Task<Metadata> _DownloadFileAsync (DownloadFileTransfer transfer) {
            // Debug.Log("_DownloadFileAsync");
            // check if transfer is already queued or in process
            // if so, subscribe to its completion
            var alreadyHave = GetQueuedOrExecutingDownloadTransfer (transfer.DropboxPath, transfer.LocalPath);
            if (alreadyHave != null) {
                return await ProcessDuplicateAsync(alreadyHave, transfer);    
            }

            // otherwise put new transfer to queue
            // lock (_transfersLock) {
                _downloadTransferQueue.Add (transfer);
            // }
            OnTransfersListChanged();
            // and subscribe to completion
            return await transfer.CompletionSource.Task;
        }

        private async Task<Metadata> _UploadFileAsync (UploadFileTransfer transfer) {
            // check if transfer is already queued or in process
            // if so, subscribe to its completion
            var alreadyHave = GetQueuedOrExecutingUploadTransfer (transfer.DropboxPath, transfer.LocalPath);
            if (alreadyHave != null) {
                return await ProcessDuplicateAsync(alreadyHave, transfer);    
            }

            // otherwise put new transfer to queue
            // lock (_transfersLock) {
                _uploadTransferQueue.Add (transfer);
            // }
            OnTransfersListChanged();
            // and subscribe to completion
            return await transfer.CompletionSource.Task;
        }

        private async Task<Metadata> ProcessDuplicateAsync(IFileTransfer originalTransfer, IFileTransfer duplicateTransfer) {
            Debug.LogWarning($"[DropboxSync/TransferManager] Duplicate {(duplicateTransfer is DownloadFileTransfer ? "download" : "upload")} trasfer.");

            EventHandler<TransferProgressReport> reportProgressToDuplicateHandler = (sender, progress) => {
                ((IProgress<TransferProgressReport>)duplicateTransfer.ProgressCallback).Report(progress);
            };

            // subscribe to progress of existing transfer
            originalTransfer.ProgressCallback.ProgressChanged += reportProgressToDuplicateHandler;
            
            // dont wait for existing task if this duplicate was canceled     
            try {
                return await originalTransfer.CompletionSource.Task.WaitOrCancel(duplicateTransfer.CancellationToken); 
            }catch(OperationCanceledException ex) {                    
                // stop sending progress to duplicate
                originalTransfer.ProgressCallback.ProgressChanged -= reportProgressToDuplicateHandler;
                throw new OperationCanceledException($"File transfer {duplicateTransfer} (duplicate) was cancelled");
            }
        }

        public bool HaveQueuedOrExecutingDownloadsRelatedTo(string dropboxPath){            
            return _currentDownloadTransfers.Any (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath))
                || _downloadTransferQueue.Any (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath));
        }

        public async Task CancelQueuedOrExecutingDownloadTransferAsync(string dropboxPath){
            // lock (_transfersLock) {
                var queued = _downloadTransferQueue.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath));
                if (queued != null) {
                    // remove from queue
                    _downloadTransferQueue.Remove(queued);
                    OnTransfersListChanged();
                }

                var executing = _currentDownloadTransfers.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath));
                if (executing != null) {                   

                    // cancel and await transfer cancellation to finish
                    executing.Cancel();

                    // Debug.LogWarning($"Awaiting executing.CompletionSource.Task of {dropboxPath}... (IsCanceled: {executing.CompletionSource.Task.IsCanceled})");
                    try {
                        await executing.CompletionSource.Task;
                    }catch(OperationCanceledException){}                    
                }                
            // }
        }

        private DownloadFileTransfer GetQueuedOrExecutingDownloadTransfer (string dropboxPath, string localPath) {
            // lock (_transfersLock) {
                var executing = _currentDownloadTransfers.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) 
                                && t.LocalPath == localPath);
                if (executing != null) {
                    return executing as DownloadFileTransfer;
                }

                var queued = _downloadTransferQueue.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) && t.LocalPath == localPath);
                if (queued != null) {
                    return queued;
                }
            // }

            return null;
        }

        private UploadFileTransfer GetQueuedOrExecutingUploadTransfer (string dropboxPath, string localPath) {
            // lock (_transfersLock) {
                var executing = _currentUploadTransfers.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) 
                                    && t.LocalPath == localPath);
                if (executing != null) {
                    return executing as UploadFileTransfer;
                }

                var queued = _uploadTransferQueue.FirstOrDefault (t => Utils.AreEqualDropboxPaths (t.DropboxPath, dropboxPath) && t.LocalPath == localPath);
                if (queued != null) {
                    return queued;
                }
            // }

            return null;
        }

        private async void ProcessTransferAsync (IFileTransfer transfer) {

            try {
                var metadata = await transfer.ExecuteAsync ();               

                // move to completed
                // lock (_transfersLock) {
                    if(transfer is DownloadFileTransfer){
                        _currentDownloadTransfers.Remove (transfer);
                    }else if(transfer is UploadFileTransfer){
                        _currentUploadTransfers.Remove (transfer);
                    }                    
                    _completedTransfers.Add (transfer);

                    Debug.Log ($"[DropboxSync/TransferManager] Transfer completed, moving to completed (now {_completedTransfers.Count} completed)");
                // }

                transfer.CompletionSource.SetResult (metadata);

                OnTransfersListChanged();
            } catch (Exception ex) {

                // if it's download trasnfer - remove *.download file
                if(transfer is DownloadFileTransfer){
                    var downloadTrasnfer = transfer as DownloadFileTransfer;
                    if(downloadTrasnfer.Metadata != null){    
                        var downloadTempPath = Utils.GetDownloadTempFilePath(transfer.LocalPath, downloadTrasnfer.Metadata.content_hash);
                        if(File.Exists(downloadTempPath)){
                            File.Delete(downloadTempPath);
                        }                        
                    }                    
                }

                // remove from current
                // lock (_transfersLock) {
                    if(transfer is DownloadFileTransfer){
                        _currentDownloadTransfers.Remove (transfer);
                    }else if(transfer is UploadFileTransfer){
                        _currentUploadTransfers.Remove (transfer);
                    }
                // }

                if(ex is OperationCanceledException){
                    // transfer cancelled
                    Debug.Log ($"[DropboxSync/TransferManager] Transfer was cancelled");                    
                }else{
                    // move to failed
                    // lock (_transfersLock) {
                        _failedTransfers.Add (transfer);
                        Debug.Log ($"[DropboxSync/TransferManager] Transfer failed, moving to failed (now {_failedTransfers.Count} failed)");
                    // }
                }
                

                transfer.SetEndDateTime(DateTime.Now);
                transfer.CompletionSource.SetException (ex);

                OnTransfersListChanged();
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
            // lock (_transfersLock) {
                _currentDownloadTransfers.ForEach (x => x.Cancel ());
                _currentUploadTransfers.ForEach (x => x.Cancel ());
            // }
        }
    }

}