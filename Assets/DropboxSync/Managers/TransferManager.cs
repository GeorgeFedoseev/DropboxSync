using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class TransferManager : IDisposable {

        private DropboxSyncConfiguration _config;

        private Queue<IFileTransfer> _transferQueue = new Queue<IFileTransfer> ();
        private List<IFileTransfer> _currentTransfers = new List<IFileTransfer> ();
        
        private List<IFileTransfer> _failedTransfers = new List<IFileTransfer> ();
        private List<IFileTransfer> _completedTransfers = new List<IFileTransfer> ();
        private object _transfersLock = new object ();

        private Thread _backgroundThread;
        private bool _isDisposed = false;

        public TransferManager (DropboxSyncConfiguration config) {
            _config = config;

            _backgroundThread = new Thread(_backgroudWorker);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start();
        }

        public void MaybeAddFromQueue () {
            lock (_transfersLock) {
                if (_currentTransfers.Count < _config.maxSimultaneousFileTransfers) {
                    // can add more
                    int canAddNum = _config.maxSimultaneousFileTransfers - _currentTransfers.Count;
                    int addNum = Math.Min(canAddNum, _transferQueue.Count);

                    if(addNum > 0){
                        Debug.Log($"[TransferManager] Adding {addNum} transfers to process");
                        for (var i = 0; i < addNum; i++) {
                            var transfer = _transferQueue.Dequeue();
                            // fire and forget
                            ProcessTransferAsync(transfer);
                            _currentTransfers.Add(transfer);
                        }
                    }
                    
                }
            }
        }

        private void _backgroudWorker(){
            while(!_isDisposed){
                MaybeAddFromQueue();
                Thread.Sleep(100);
            }
        }

        // METHODS

        public async Task<FileMetadata> DownloadFileAsync (string dropboxPath, string localPath, IProgress<int> progressCallback) {
            var completionSource = new TaskCompletionSource<FileMetadata> ();

            var downloadTransfer = new DownloadFileTransfer (dropboxPath, localPath, progressCallback, completionSource, _config);

            lock (_transfersLock) {
                _transferQueue.Enqueue (downloadTransfer);
            }

            return await completionSource.Task;
        }

        private async void ProcessTransferAsync (IFileTransfer transfer) {

            try {
                var metadata = await transfer.ExecuteAsync ();
                transfer.CompletionSource.SetResult (metadata);
                
                // move to completed
                lock(_transfersLock){
                    _currentTransfers.Remove(transfer);
                    _completedTransfers.Add(transfer);

                    Debug.Log($"[TransferManager] Transfer completed, moving to completed (now {_completedTransfers.Count} completed)");
                }
            } catch (Exception ex) {
                transfer.CompletionSource.SetException (ex);                
                // move to failed
                lock(_transfersLock){
                    _currentTransfers.Remove(transfer);
                    _failedTransfers.Add(transfer);

                    Debug.Log($"[TransferManager] Transfer failed, moving to failed (now {_failedTransfers.Count} failed)");
                }

                
            }

        }

        

        public void Dispose () {
            _isDisposed = true;
        }
    }

}