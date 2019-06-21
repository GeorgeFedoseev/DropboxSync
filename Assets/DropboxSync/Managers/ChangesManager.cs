using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class FileChangeSubscription {
        public string fileParentFolderPath;
        public Action<EntryChange> folderChangeCallback;
        public Action<EntryChange> fileChangeCallback;
    }

    public class SyncSubscription {
        public Action<EntryChange> changedCallback;
        public List<Action<EntryChange>> syncedCallbacks = new List<Action<EntryChange>>();
        public CancellationTokenSource syncCancellationTokenSource = new CancellationTokenSource();
    }

    public class ChangesManager {


        private CacheManager _cacheManager;
        private DropboxSyncConfiguration _config;

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        // keep track of last cursor to longpoll on it for changes in whole account
        private string _lastCursor;

        private Dictionary<string, List<Action<EntryChange>>> _folderSubscriptions = new Dictionary<string, List<Action<EntryChange>>>();
        private Dictionary<string, string> _folderCursors = new Dictionary<string, string>();        

        private Dictionary<string, List<FileChangeSubscription>> _fileSubscriptions = new Dictionary<string, List<FileChangeSubscription>>();

        private Dictionary<string, SyncSubscription> _syncSubscriptions = new Dictionary<string, SyncSubscription>();        

        public ChangesManager (CacheManager cacheManager, DropboxSyncConfiguration config) {
            _cacheManager = cacheManager;
            _config = config;

            _backgroundThread = new Thread (_backgroudWorker);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start ();
        }
        

        private async void _backgroudWorker () {
            while (!_isDisposed) {
                // if _lastCursor != null start longpoll request on this cursor
                if(_lastCursor != null){
                    try {
                        Debug.LogWarning("Do longpoll request...");
                        var longpollResponse = await new ListFolderLongpollRequest(new ListFolderLongpollRequestParameters {
                            cursor = _lastCursor
                        }, _config).ExecuteAsync();

                        Debug.LogWarning($"Longpoll response: {longpollResponse}");
                        
                        if(longpollResponse.changes){
                            await CheckChangesInFoldersAsync();                             
                        }

                        if(longpollResponse.backoff > 0){
                            Debug.LogWarning($"longpollResponse.backoff = {longpollResponse.backoff}");
                        }
                        
                        // wait before making next longpoll
                        Thread.Sleep(longpollResponse.backoff * 10000);

                    }catch(DropboxResetCursorAPIException cursorResetException){
                        // if exception is because cursor is not valid anymore do CheckChangesInFoldersAsync() to get new cursor
                        await CheckChangesInFoldersAsync();                        
                    }catch(Exception ex){                        
                        Debug.LogError($"Failed to request Dropbox changes: {ex}");
                        Thread.Sleep(_config.requestErrorRetryDelaySeconds * 1000);                                                
                    }                    
                }
                
                // if request returned changes = true call CheckChangesInFolders
                // else start long poll again (after backoff timeout if needed)
                Thread.Sleep (100);
            }
        }

        // SYNCRONIZATION

        public bool IsKeepingInSync(string dropboxPath){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            return _syncSubscriptions.ContainsKey(dropboxPath);
        }

        public void KeepSynced(string dropboxPath, Action<EntryChange> callback){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            // start syncing if not already
            if(!_syncSubscriptions.ContainsKey(dropboxPath)){
                _syncSubscriptions[dropboxPath] = new SyncSubscription();

                Action<EntryChange> changedCallback = async (change) => {
                    // sync
                    // Debug.Log(change);

                    try {
                        
                        await _cacheManager.SyncChangeAsync(change, new Progress<TransferProgressReport>((progress) => {
                            // Debug.Log($"Syncing {dropboxPath} {progress.progress}% {progress.bytesPerSecondFormatted}");
                        }), _syncSubscriptions[dropboxPath].syncCancellationTokenSource.Token);

                        // report that synced
                        callback(change);

                    }catch(DropboxNotFoundAPIException){
                        Debug.LogWarning($"[DropboxSync/KeepSynced] Didn't find file {change.metadata.path_display} during sync. Probably it was deleted on Dropbox during sync operation.");
                    }catch(OperationCanceledException){
                        // quiet
                    }catch(Exception ex){
                        // reset syncing
                        Debug.LogWarning($"Resetting syncing of {dropboxPath} due to {ex}");
                        ResetSyncing(dropboxPath);
                    }
                };                
                
                _syncSubscriptions[dropboxPath] = new SyncSubscription {                    
                    changedCallback = changedCallback
                };

                SubscribeToChanges(dropboxPath, changedCallback);
            }

            // add callback
            if(!_syncSubscriptions[dropboxPath].syncedCallbacks.Contains(callback)){
                _syncSubscriptions[dropboxPath].syncedCallbacks.Add(callback);
            }            
        }

        public void StopKeepingInSync(string dropboxPath){
            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);

            if(_syncSubscriptions.ContainsKey(dropboxPath)){
                var sub = _syncSubscriptions[dropboxPath];
                // cancel current file transfers
                sub.syncCancellationTokenSource.Cancel();
                // unsubscribe from changes notifications
                UnsubscribeFromChanges(dropboxPath, sub.changedCallback);
                _syncSubscriptions.Remove(dropboxPath);
            }
        }

        private void ResetSyncing(string dropboxPath){
            if(_syncSubscriptions.ContainsKey(dropboxPath)){

                StopKeepingInSync(dropboxPath);

                var oldSub = _syncSubscriptions[dropboxPath];                

                // start again and add all previous callbacks
                foreach(var callback in oldSub.syncedCallbacks){
                    KeepSynced(dropboxPath, callback);
                }
            }
        }

        // CHANGES NOTIFICATIONS
        public async void SubscribeToChanges(string dropboxPath, Action<EntryChange> callback){
            Debug.LogWarning($"Check if a file or a folder {dropboxPath}");
            var metadata = (await new GetMetadataRequest(dropboxPath, _config).ExecuteAsync()).GetMetadata();
            if(metadata.IsFile){
                SubscribeToFileChages(dropboxPath, callback);
            }else if(metadata.IsFolder){
                SubscribeToFolderChanges(dropboxPath, callback);
            }
        }

        public void UnsubscribeFromChanges(string dropboxPath, Action<EntryChange> callback){
            // no need to check if file or folder, can do both
            UnsubscribeFromFileChanges(dropboxPath, callback);
            UnsubscribeFromFolderChanges(dropboxPath, callback);
        }

        private void SubscribeToFileChages(string dropboxFilePath, Action<EntryChange> callback){
            dropboxFilePath = Utils.UnifyDropboxPath(dropboxFilePath);

            Debug.Log($"SubscribeToFileChages {dropboxFilePath}");

            // get folder path from file path
            var dropboxFolderPath = Path.GetDirectoryName(dropboxFilePath);
            Action<EntryChange> folderChangeCallback = (change) => {
                if(Utils.AreEqualDropboxPaths(change.metadata.path_lower, dropboxFilePath)){
                    callback(change);
                }
            };
            
            if(!_fileSubscriptions.ContainsKey(dropboxFilePath)){
                _fileSubscriptions[dropboxFilePath] = new List<FileChangeSubscription>();
            }

            // associate file path with subscription
            if(!_fileSubscriptions[dropboxFilePath].Any(sub => sub.fileChangeCallback == callback)){
                _fileSubscriptions[dropboxFilePath].Add(new FileChangeSubscription {
                    fileParentFolderPath = dropboxFolderPath,
                    folderChangeCallback = folderChangeCallback,
                    fileChangeCallback = callback
                });
            }            

            SubscribeToFolderChanges(dropboxFolderPath, folderChangeCallback);
        }

        private void UnsubscribeFromFileChanges(string dropboxFilePath, Action<EntryChange> callback){
            dropboxFilePath = Utils.UnifyDropboxPath(dropboxFilePath);

            if(_fileSubscriptions.ContainsKey(dropboxFilePath)){
                // unsubscribe associated folder change callbacks
                _fileSubscriptions[dropboxFilePath].Where(sub => sub.fileChangeCallback == callback).ToList().ForEach(sub => {
                    UnsubscribeFromFolderChanges(sub.fileParentFolderPath, sub.folderChangeCallback);
                });
                _fileSubscriptions[dropboxFilePath].RemoveAll(sub => sub.fileChangeCallback == callback);
            }

        }

        private async void SubscribeToFolderChanges(string dropboxFolderPath, Action<EntryChange> callback){
            dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);

            Debug.LogWarning($"SubscribeToFolderChanges {dropboxFolderPath}");

            // add folder to dictionary
            if(!_folderSubscriptions.ContainsKey(dropboxFolderPath)){
                _folderSubscriptions[dropboxFolderPath] = new List<Action<EntryChange>>();
            }
            
            // associate folder with callback
            if(!_folderSubscriptions[dropboxFolderPath].Contains(callback)){
                _folderSubscriptions[dropboxFolderPath].Add(callback);
            }            

            ResetCursorForFolderAsync(dropboxFolderPath);
            await CheckChangesInFolderAsync(dropboxFolderPath);
        }      

        private void UnsubscribeFromFolderChanges(string dropboxFolderPath, Action<EntryChange> callback){
            dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);

            if(_folderSubscriptions.ContainsKey(dropboxFolderPath)){
                if(_folderSubscriptions[dropboxFolderPath].Contains(callback)){
                    _folderSubscriptions[dropboxFolderPath].Remove(callback);
                }

                // if no one listening - no reason to check this folder for changes - remove key from dictionary
                if(_folderSubscriptions[dropboxFolderPath].Count == 0){
                    _folderSubscriptions.Remove(dropboxFolderPath);

                    // if no folders left - dont't do longpoll
                    if(_folderSubscriptions.Count == 0){
                        _lastCursor = null;
                    }
                }
            }
        }

        // called from longpoll thread when changes = true
        private async Task CheckChangesInFoldersAsync(){
            var folders = _folderSubscriptions.Select(x => x.Key);
            foreach(var folder in folders){
                await CheckChangesInFolderAsync(folder);
            }
        }      

        // called from longpoll when changes = true or after adding new folder subscription 
        private async Task CheckChangesInFolderAsync(string dropboxFolderPath){
            string cursor = null;
            bool has_more = true;

            // if was already listing folder - continue from there
            if(_folderCursors.ContainsKey(dropboxFolderPath)){
                cursor = _folderCursors[dropboxFolderPath];
            }

            if(cursor == null){
                // list_folder 
                var listFolderResponse = await new ListFolderRequest(new ListFolderRequestParameters {
                    path = dropboxFolderPath,
                    recursive = true,
                    include_deleted = true                
                }, _config).ExecuteAsync();

                // process entries
                listFolderResponse.entries.ForEach(entry => ProcessReceivedMetadataForFolder(dropboxFolderPath, entry));
                
                has_more = listFolderResponse.has_more;
                cursor = listFolderResponse.cursor;
            }
            
            while(has_more){
                Debug.LogWarning("CheckChangesInFolder: has_more");

                // list_folder/continue
                ListFolderResponse listFolderContinueResponse;
                try {
                    listFolderContinueResponse = await new ListFolderContinueRequest(new CursorRequestParameters {
                        cursor = cursor
                    }, _config).ExecuteAsync();                        

                    // process entries
                    listFolderContinueResponse.entries.ForEach(entry => ProcessReceivedMetadataForFolder(dropboxFolderPath, entry));
                    
                    has_more = listFolderContinueResponse.has_more;
                    cursor = listFolderContinueResponse.cursor;

                }catch(DropboxResetCursorAPIException ex){
                    Debug.LogWarning($"[DropboxSync] Resetting cursor for folder {dropboxFolderPath}");

                    // cursor is invalid - need to reset it
                    ResetCursorForFolderAsync(dropboxFolderPath);

                    // start listing folder from beginning
                    await CheckChangesInFolderAsync(dropboxFolderPath);
                }                
            }

            // save latest cursor to the folder
            _folderCursors[dropboxFolderPath] = cursor;
            // update _lastCursor for next longpoll
            _lastCursor = cursor;
        }

        private void ResetCursorForFolderAsync(string dropboxFolderPath){
            if(_folderCursors.ContainsKey(dropboxFolderPath)){
                _folderCursors.Remove(dropboxFolderPath);                
            }            
        }

        private void ProcessReceivedMetadataForFolder(string dropboxFolderPath, Metadata remoteMetadata){
            dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);
            // detect changes based on local metadata
            // call FileChange events for subscriptions if detected changes

            // Debug.Log($"Process remote metadata: {remoteMetadata}");

            if(remoteMetadata.EntryType == DropboxEntryType.File){
                // file created or modified
                if(_cacheManager.HaveFileLocally(remoteMetadata)){
                    if(_cacheManager.ShouldUpdateFileFromDropbox(remoteMetadata)){
                        // file modified
                        _folderSubscriptions[dropboxFolderPath].ForEach(a => a(new EntryChange {
                            type = EntryChangeType.Modified,
                            metadata = remoteMetadata
                        }));
                    }
                }else{
                    // file created
                    _folderSubscriptions[dropboxFolderPath].ForEach(a => a(new EntryChange {
                        type = EntryChangeType.Created,
                        metadata = remoteMetadata
                    }));
                }
            }else if(remoteMetadata.EntryType == DropboxEntryType.Deleted){
                // can be folder or file path here, but we ignore folders:
                // if path will be folder then HaveFileLocally will return false and we do nothing

                // check if we also need to delete file
                if(_cacheManager.HaveFileLocally(remoteMetadata)){                    
                    _folderSubscriptions[dropboxFolderPath].ForEach(a => a(new EntryChange {
                        type = EntryChangeType.Removed,
                        metadata = remoteMetadata
                    }));
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