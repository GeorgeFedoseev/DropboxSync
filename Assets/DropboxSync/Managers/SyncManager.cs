using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DBXSync {

    public class SyncSubscription {
        public string dropboxPath;
        public Action<EntryChange> changedCallback;
        public List<Action<EntryChange>> syncedCallbacks = new List<Action<EntryChange>>();
        public CancellationTokenSource syncCancellationTokenSource = new CancellationTokenSource();
    }

    public class SyncManager : IDisposable {

        private CacheManager _cacheManager;
        private ChangesManager _changesManager;
        private DropboxSyncConfiguration _config;

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;
            
        private List<SyncSubscription> _syncStartQueue = new List<SyncSubscription>();
        private Dictionary<string, SyncSubscription> _syncSubscriptions = new Dictionary<string, SyncSubscription>();

        private object _syncSubscriptionsLock = new object();    

        public SyncManager (CacheManager cacheManager, ChangesManager changesManager, DropboxSyncConfiguration config) {
            _cacheManager = cacheManager;
            _changesManager = changesManager;
            _config = config;

            _backgroundThread = new Thread (_backgroudWorker);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start ();
        }
        

        private void _backgroudWorker () {
            while (!_isDisposed) {
                lock(_syncSubscriptionsLock) {
                    foreach(var sub in _syncStartQueue.ToList()){
                        _syncStartQueue.Remove(sub); // dequeue
                        _StartSync(sub);
                    }
                }                
                Thread.Sleep(1000);
            }
        }

        // SYNCRONIZATION

        public bool IsKeepingInSync(string dropboxPath){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            return _syncSubscriptions.ContainsKey(dropboxPath);
        }

        public void KeepSynced(string dropboxPath, Action<EntryChange> callback){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            // if already syncing - add callback to subscription
            if(IsKeepingInSync(dropboxPath)){                
                // add callback
                if(!_syncSubscriptions[dropboxPath].syncedCallbacks.Contains(callback)){
                    _syncSubscriptions[dropboxPath].syncedCallbacks.Add(callback);
                }  
            }
            // if in queue to start - add callback to subscription
            else if(_syncStartQueue.Any(sub => Utils.AreEqualDropboxPaths(sub.dropboxPath, dropboxPath))){
                var queuedSub = _syncStartQueue.First(sub => Utils.AreEqualDropboxPaths(sub.dropboxPath, dropboxPath));
                if(!queuedSub.syncedCallbacks.Contains(callback)){
                    queuedSub.syncedCallbacks.Add(callback);
                }                
            }
            // if not keeping in sync and not in queue to start - create sync subscription put to queue
            else {
                var syncSubscription = new SyncSubscription();
                syncSubscription.dropboxPath = dropboxPath;
                syncSubscription.syncedCallbacks.Add(callback);
                _syncStartQueue.Add(syncSubscription);
            }                     
        }

        private async void _StartSync(SyncSubscription syncSubscription){
            var dropboxPath = syncSubscription.dropboxPath;

            Action<EntryChange> changedCallback = async (change) => {
                // sync
                // Debug.Log(change);

                try {
                    
                    await _cacheManager.SyncChangeAsync(change, new Progress<TransferProgressReport>((progress) => {
                        // Debug.Log($"Syncing {dropboxPath} {progress.progress}% {progress.bytesPerSecondFormatted}");
                    }), _syncSubscriptions[dropboxPath].syncCancellationTokenSource.Token);

                    // report to all callbacks that synced
                    if(_syncSubscriptions.ContainsKey(dropboxPath) && _syncSubscriptions[dropboxPath] == syncSubscription){
                        foreach(var callback in _syncSubscriptions[dropboxPath].syncedCallbacks){
                            callback(change);
                        }
                    }

                }catch(DropboxNotFoundAPIException){
                    Debug.LogWarning($"[DropboxSync/KeepSynced] Didn't find file {change.metadata.path_display} during sync. Probably it was deleted on Dropbox during sync operation.");
                }catch(OperationCanceledException){
                    // quiet
                }catch(Exception ex){
                    // reset syncing
                    Debug.LogError($"Failed to sync change {change}; sync subscription: {syncSubscription.GetHashCode()}");
                    // check if that subscription still going (cause can be already canceled by other transfer errors)
                    if(_syncSubscriptions.ContainsKey(dropboxPath) && _syncSubscriptions[dropboxPath] == syncSubscription){
                        Debug.LogWarning($"Resetting syncing of {dropboxPath} due to {ex}");
                        // reset syncing
                        ResetSyncing(dropboxPath);
                    }                        
                }
            };                
            
            try {
                await _changesManager.SubscribeToChanges(dropboxPath, changedCallback);

                syncSubscription.changedCallback = changedCallback;

                _syncSubscriptions[dropboxPath] = syncSubscription;
            }catch(Exception ex){
                Debug.Log($"Failed to start sync of {dropboxPath}; Put back in start queue\nException: {ex}");
                lock(_syncSubscriptionsLock){
                    _syncStartQueue.Add(syncSubscription);
                }
            }
            
        }

        public void StopKeepingInSync(string dropboxPath){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            if(_syncSubscriptions.ContainsKey(dropboxPath)){
                var sub = _syncSubscriptions[dropboxPath];
                // cancel current file transfers
                sub.syncCancellationTokenSource.Cancel();
                // unsubscribe from changes notifications
                _changesManager.UnsubscribeFromChanges(dropboxPath, sub.changedCallback);
                _syncSubscriptions.Remove(dropboxPath);
            }

            // remove from queue
            _syncStartQueue.RemoveAll(sub => Utils.AreEqualDropboxPaths(dropboxPath, sub.dropboxPath));
        }

        private void ResetSyncing(string dropboxPath){
            if(_syncSubscriptions.ContainsKey(dropboxPath)){
                
                var oldSub = _syncSubscriptions[dropboxPath];

                StopKeepingInSync(dropboxPath);                

                // start again and add all previous callbacks
                foreach(var callback in oldSub.syncedCallbacks){
                    KeepSynced(dropboxPath, callback);
                }
            }
        }

        private void CancelAllCurrentSyncTransfers(){
            foreach(var kv in _syncSubscriptions){
                var sub = kv.Value;
                sub.syncCancellationTokenSource.Cancel();
            }
        }


        public void Dispose () {            
            _isDisposed = true;      
            CancelAllCurrentSyncTransfers();
        }
    }
}