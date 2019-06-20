using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class ChangesManager {


        private CacheManager _cacheManager;
        private DropboxSyncConfiguration _config;

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        // keep track of last cursor to longpoll on it for changes in whole account
        private string _lastCursor;

        private Dictionary<string, List<Action<EntryChange>>> _folderSubscriptions = new Dictionary<string, List<Action<EntryChange>>>();
        private Dictionary<string, string> _folderCursors = new Dictionary<string, string>();        

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

        public void SubscribeToFolder(string dropboxFolderPath, Action<EntryChange> callback){
            dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);

            // add folder to dictionary
            if(!_folderSubscriptions.ContainsKey(dropboxFolderPath)){
                _folderSubscriptions[dropboxFolderPath] = new List<Action<EntryChange>>();
            }
            
            // associate folder with callback
            _folderSubscriptions[dropboxFolderPath].Add(callback);

            CheckChangesInFolder(dropboxFolderPath);
        }      

        // called from longpoll thread when changes = true
        private async Task CheckChangesInFoldersAsync(){
            var folders = _folderSubscriptions.Select(x => x.Key);
            foreach(var folder in folders){
                await CheckChangesInFolderAsync(folder);
            }
        }

        private async void CheckChangesInFolder(string dropboxFolderPath){
            await CheckChangesInFolderAsync(dropboxFolderPath);
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
                    _folderCursors.Remove(dropboxFolderPath);

                    // start listing folder from beginning
                    await CheckChangesInFolderAsync(dropboxFolderPath);                                        
                }                
            }

            // save latest cursor to the folder
            _folderCursors[dropboxFolderPath] = cursor;
            // update _lastCursor for next longpoll
            _lastCursor = cursor;
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


        public void Dispose () {            
            _isDisposed = true;            
        }
    }
    
}