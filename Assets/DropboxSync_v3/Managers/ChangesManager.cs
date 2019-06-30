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
        public List<Action<EntryChange>> fileChangeCallbacks = new List<Action<EntryChange>>();
    }

    public class PathChangeSubscription {
        public string dropboxPath;
        public List<Action<EntryChange>> callbacks = new List<Action<EntryChange>>();
    }
    

    public class ChangesManager : IDisposable {


        private CacheManager _cacheManager;
        private TransferManager _transferManager;
        private DropboxSyncConfiguration _config;

        // private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        // keep track of last cursor to longpoll on it for changes in whole account
        private string _lastCursor;

        private Dictionary<string, List<Action<EntryChange>>> _folderSubscriptions = new Dictionary<string, List<Action<EntryChange>>>();
        private Dictionary<string, string> _folderCursors = new Dictionary<string, string>();        

        private Dictionary<string, FileChangeSubscription> _fileSubscriptions = new Dictionary<string, FileChangeSubscription>();

        private List<PathChangeSubscription> _pathSubscriptionQueue = new List<PathChangeSubscription>();

        public ChangesManager (CacheManager cacheManager, TransferManager transferManager, DropboxSyncConfiguration config) {
            _cacheManager = cacheManager;
            _transferManager = transferManager;
            _config = config;

            // _backgroundThread = new Thread (_backgroudWorker);
            // _backgroundThread.IsBackground = true;
            // _backgroundThread.Start ();
            _longpollSpinner();
            _pathSubscriptionSpinner();
        }


        private async void _pathSubscriptionSpinner(){
            while (!_isDisposed) {
                // process subscription queue
                foreach(var sub in _pathSubscriptionQueue.ToList()){
                    try {
                        // Debug.LogWarning($"[D][DropboxSync/ChangesManager] Get metadata for path sub {sub.dropboxPath}");

                        var metadata = (await new GetMetadataRequest(sub.dropboxPath, _config).ExecuteAsync()).GetMetadata();

                        // dequeue if got metadata
                        _pathSubscriptionQueue.Remove(sub);

                        // Debug.LogWarning($"[D][DropboxSync/ChangesManager] Got metadata for path sub {sub.dropboxPath}");

                        foreach(var callback in sub.callbacks){
                            if(metadata.IsFile){
                                SubscribeToFileChanges(sub.dropboxPath, callback);
                            }else if(metadata.IsFolder){
                                SubscribeToFolderChanges(sub.dropboxPath, callback);
                            }
                        }                        
                    }catch(Exception ex){
                        Debug.LogWarning($"[DropboxSync/ChangesManager] Failed to subscribe for changes on path {sub.dropboxPath}\n{ex}. Retrying in {_config.pathSubscriptionFailedDelaySeconds} seconds...");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_config.pathSubscriptionFailedDelaySeconds));
            }
        }
        

        private async void _longpollSpinner () {
            while (!_isDisposed) {               

                // if _lastCursor != null start longpoll request on this cursor
                if(_lastCursor != null){
                    try {
                        // Debug.LogWarning("Do longpoll request...");
                        var longpollResponse = await new ListFolderLongpollRequest(new ListFolderLongpollRequestParameters {
                            cursor = _lastCursor
                        }, _config).ExecuteAsync();

                        // Debug.LogWarning($"Longpoll response: {longpollResponse}");
                        
                        if(longpollResponse.changes){
                            await CheckChangesInFoldersAsync();                             
                        }

                        if(longpollResponse.backoff > 0){
                            // Debug.LogWarning($"longpollResponse.backoff = {longpollResponse.backoff}");
                        }
                        
                        // wait before making next longpoll
                        await Task.Delay (longpollResponse.backoff * 10000);

                    }catch(DropboxResetCursorAPIException){
                        // if exception is because cursor is not valid anymore do CheckChangesInFoldersAsync() to get new cursor
                        await CheckChangesInFoldersAsync();
                    }catch(Exception ex){                        
                        // Debug.LogError($"Failed to request Dropbox changes: {ex}");
                        await Task.Delay (TimeSpan.FromSeconds(_config.requestErrorRetryDelaySeconds));
                    }                    
                }else if(_folderSubscriptions.Count > 0){
                    // need to get cursor
                    await CheckChangesInFoldersAsync();
                }
                
                // if request returned changes = true call CheckChangesInFolders
                // else start long poll again (after backoff timeout if needed)
                await Task.Delay (100);
            }
        }

        

        // CHANGES NOTIFICATIONS
        public void SubscribeToChanges(string dropboxPath, Action<EntryChange> callback){
            // Debug.Log($"[DropboxSync/ChangesManager] SubscribeToChanges {dropboxPath}");

            dropboxPath = Utils.UnifyDropboxPath(dropboxPath);
            if(_folderSubscriptions.ContainsKey(dropboxPath)){
                if(!_folderSubscriptions[dropboxPath].Contains(callback)){
                    _folderSubscriptions[dropboxPath].Add(callback);
                }                
            }else if(_fileSubscriptions.ContainsKey(dropboxPath)){
                if(!_fileSubscriptions[dropboxPath].fileChangeCallbacks.Contains(callback)){
                    _fileSubscriptions[dropboxPath].fileChangeCallbacks.Add(callback);
                }
            }else if(_pathSubscriptionQueue.Any(s => Utils.AreEqualDropboxPaths(s.dropboxPath, dropboxPath))){
                var sub = _pathSubscriptionQueue.First(s => Utils.AreEqualDropboxPaths(s.dropboxPath, dropboxPath));
                if(!sub.callbacks.Contains(callback)){
                    sub.callbacks.Add(callback);
                }                
            }else{
                // create new sub and add to queue
                var sub = new PathChangeSubscription();
                sub.dropboxPath = dropboxPath;
                sub.callbacks.Add(callback);
                _pathSubscriptionQueue.Add(sub);
            }            
        }

        public void UnsubscribeFromChanges(string dropboxPath, Action<EntryChange> callback){
            // remove from queue if there
            var sub = _pathSubscriptionQueue.Where(x => Utils.AreEqualDropboxPaths(x.dropboxPath, dropboxPath)).FirstOrDefault();
            if(sub != null){
                sub.callbacks.Remove(callback);
                if(sub.callbacks.Count == 0){
                    _pathSubscriptionQueue.Remove(sub);
                }
            }

            // no need to check if file or folder, can do both
            UnsubscribeFromFileChanges(dropboxPath, callback);
            UnsubscribeFromFolderChanges(dropboxPath, callback);
        }

        private void SubscribeToFileChanges(string dropboxFilePath, Action<EntryChange> callback){
            dropboxFilePath = Utils.UnifyDropboxPath(dropboxFilePath);

            // Debug.Log($"SubscribeToFileChages {dropboxFilePath}");            
            
            if(!_fileSubscriptions.ContainsKey(dropboxFilePath)){
                // get folder path from file path
                var dropboxFolderPath = Path.GetDirectoryName(dropboxFilePath);

                Action<EntryChange> folderChangeCallback = (change) => {
                    //  Debug.LogWarning($"SubscribeToFileChages {dropboxFilePath} folder change {change}");  
                    if(Utils.AreEqualDropboxPaths(change.metadata.path_lower, dropboxFilePath)){
                        _fileSubscriptions[dropboxFilePath].fileChangeCallbacks.ForEach(c => c(change));
                    }
                };

                _fileSubscriptions[dropboxFilePath] = new FileChangeSubscription {
                    fileParentFolderPath = dropboxFolderPath,
                    folderChangeCallback = folderChangeCallback
                };

                SubscribeToFolderChanges(dropboxFolderPath, folderChangeCallback);
            }

            // associate file path with subscription
            if(!_fileSubscriptions[dropboxFilePath].fileChangeCallbacks.Contains(callback)){
                _fileSubscriptions[dropboxFilePath].fileChangeCallbacks.Add(callback);
            }            
        }

        private void UnsubscribeFromFileChanges(string dropboxFilePath, Action<EntryChange> callback){
            dropboxFilePath = Utils.UnifyDropboxPath(dropboxFilePath);

            if(_fileSubscriptions.ContainsKey(dropboxFilePath)){

                _fileSubscriptions[dropboxFilePath].fileChangeCallbacks.Remove(callback);

                if(_fileSubscriptions[dropboxFilePath].fileChangeCallbacks.Count == 0){
                    // unsubscribe associated folder change callback                
                    UnsubscribeFromFolderChanges(_fileSubscriptions[dropboxFilePath].fileParentFolderPath, _fileSubscriptions[dropboxFilePath].folderChangeCallback);
                    _fileSubscriptions.Remove(dropboxFilePath);
                }
            }

        }

        private async void SubscribeToFolderChanges(string dropboxFolderPath, Action<EntryChange> callback){
            dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);

            // Debug.LogWarning($"[D][DropboxSync/ChangesManager] SubscribeToFolderChanges {dropboxFolderPath}");

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
            // Debug.LogWarning("[D][DropboxSync/ChangesManager] CheckChangesInFoldersAsync");
            var folders = _folderSubscriptions.Select(x => x.Key);
            foreach(var folder in folders.ToList()){
                await CheckChangesInFolderAsync(folder);
            }
        }      

        // called from longpoll when changes = true or after adding new folder subscription 
        private async Task CheckChangesInFolderAsync(string dropboxFolderPath){
            // Debug.LogWarning($"[D][DropboxSync/ChangesManager] CheckChangesInFolderAsync {dropboxFolderPath}");
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
                    // Debug.LogWarning($"[DropboxSync] Resetting cursor for folder {dropboxFolderPath}");

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

            // Debug.LogWarning($"CheckChangesInFolderAsync {dropboxFolderPath}. Done.");
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

//            Debug.Log($"Process remote metadata: {remoteMetadata}");

            if(_folderSubscriptions.ContainsKey(dropboxFolderPath)){
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

                    // check if we also need to delete file or cancel related transfers
                    if(_cacheManager.HaveFileLocally(remoteMetadata) || _transferManager.HaveQueuedOrExecutingDownloadsRelatedTo(remoteMetadata.path_lower)){                    
                        _folderSubscriptions[dropboxFolderPath].ForEach(a => a(new EntryChange {
                            type = EntryChangeType.Removed,
                            metadata = remoteMetadata
                        }));
                    }
                }
            }

            
        }


        public void Dispose () {            
            _isDisposed = true;            
        }
    }
    
}