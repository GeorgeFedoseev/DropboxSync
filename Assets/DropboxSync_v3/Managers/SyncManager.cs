using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // private Thread _backgroundThread;
        private volatile bool _isDisposed = false;
            
        // private List<SyncSubscription> _syncStartQueue = new List<SyncSubscription>();
        private Dictionary<string, SyncSubscription> _syncSubscriptions = new Dictionary<string, SyncSubscription>();

        // private object _syncSubscriptionsLock = new object();    

        public SyncManager (CacheManager cacheManager, ChangesManager changesManager, DropboxSyncConfiguration config) {
            _cacheManager = cacheManager;
            _changesManager = changesManager;
            _config = config;
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
            } else {
                var syncSubscription = new SyncSubscription();
                syncSubscription.dropboxPath = dropboxPath;
                syncSubscription.syncedCallbacks.Add(callback);
                _StartSync(syncSubscription);
            }                     
        }

        private void _StartSync(SyncSubscription syncSubscription){            
            var dropboxPath = syncSubscription.dropboxPath;
            // Debug.Log($"_StartSync {dropboxPath}");

            Action<EntryChange> changedCallback = async (change) => {
                // sync
                // Debug.Log(change);

                try {
                    await _cacheManager.SyncChangeAsync(change, new Progress<TransferProgressReport>((progress) => {
                        //Debug.Log($"Syncing {dropboxPath} {progress.progress}% {progress.bytesPerSecondFormatted}");
                    }), _syncSubscriptions[dropboxPath].syncCancellationTokenSource.Token);

                    // report to all callbacks that synced
                    if(_syncSubscriptions.ContainsKey(dropboxPath) && _syncSubscriptions[dropboxPath] == syncSubscription){
                        foreach(var callback in _syncSubscriptions[dropboxPath].syncedCallbacks){
                            callback(change);
                        }
                    }

                }catch(DropboxNotFoundAPIException){
                    Debug.LogWarning($"[DropboxSync/SyncManager] Didn't find file {change.metadata.path_display} during sync. Probably it was deleted on Dropbox during sync operation.");
                }catch(OperationCanceledException){
                    // quiet
                }catch(Exception ex){
                    // reset syncing
                    // Debug.LogWarning($"[DropboxSync/SyncManager] Failed to sync change {ex}\n {change}; sync subscription: {syncSubscription.GetHashCode()}");
                    // check if that subscription still going (cause can be already canceled by other transfer errors)
                    
                    bool needsReset = _syncSubscriptions.ContainsKey(dropboxPath) && _syncSubscriptions[dropboxPath] == syncSubscription;
                    
                    if(needsReset){
                        Debug.LogWarning($"[DropboxSync/SyncManager] Resetting syncing of {dropboxPath} due to {ex}");
                        // reset syncing
                        ResetSyncing(dropboxPath);
                    }                        
                }
            };                
            
            
            _changesManager.SubscribeToChanges(dropboxPath, changedCallback);

            syncSubscription.changedCallback = changedCallback;                
            _syncSubscriptions[dropboxPath] = syncSubscription;
        }

        public void StopKeepingInSync(string dropboxPath){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            // Debug.LogWarning($"StopKeepingInSync {dropboxPath}");

            if(_syncSubscriptions.ContainsKey(dropboxPath)){
            
                var sub = _syncSubscriptions[dropboxPath];
                // cancel current file transfers
                sub.syncCancellationTokenSource.Cancel();
                // unsubscribe from changes notifications
                _changesManager.UnsubscribeFromChanges(dropboxPath, sub.changedCallback);
            
                _syncSubscriptions.Remove(dropboxPath);
            }
        }

        public void UnsubscribeFromKeepSyncCallback(string dropboxPath, Action<EntryChange> callback){
            // Debug.LogWarning($"UnsubscribeFromKeepSyncCallback {dropboxPath}");
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            if(_syncSubscriptions.ContainsKey(dropboxPath)){
                var sub = _syncSubscriptions[dropboxPath];
                if(sub.syncedCallbacks.Contains(callback)){
                    sub.syncedCallbacks.Remove(callback);
                    // check if anyone is listening
                    if(sub.syncedCallbacks.Count == 0){
                        // stop keeping in sync cause no one interested
                        StopKeepingInSync(dropboxPath);
                    }
                }
            }
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