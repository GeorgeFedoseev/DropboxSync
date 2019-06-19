using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class ChangesManager {

        private DropboxSyncConfiguration _config;

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        // keep track of last cursor to longpoll on it for changes in whole account
        private string _lastCursor;

        private Dictionary<string, List<Action<EntryChange>>> _folderSubscriptions = new Dictionary<string, List<Action<EntryChange>>>();
        private Dictionary<string, string> _folderCursors = new Dictionary<string, string>();

        private Task _checkChangesInFoldersCurrentTask = null;

        public ChangesManager (DropboxSyncConfiguration config) {
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
                            // check what changes happened
                            if(_checkChangesInFoldersCurrentTask != null){
                                await _checkChangesInFoldersCurrentTask;
                            }

                            _checkChangesInFoldersCurrentTask = CheckChangesInFoldersAsync();
                            await _checkChangesInFoldersCurrentTask;
                        }

                        if(longpollResponse.backoff > 0){
                            Debug.LogWarning($"longpollResponse.backoff = {longpollResponse.backoff}");
                        }
                        
                        // wait before making next longpoll
                        Thread.Sleep(longpollResponse.backoff * 10000);

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
            // add folder to dictionary
            if(!_folderSubscriptions.ContainsKey(dropboxFolderPath)){
                _folderSubscriptions[dropboxFolderPath] = new List<Action<EntryChange>>();
            }
            
            // associate folder with callback
            _folderSubscriptions[dropboxFolderPath].Add(callback);

            CheckChangesInFolders();
        }

        private async void CheckChangesInFolders(){
            if(_checkChangesInFoldersCurrentTask != null){
                await _checkChangesInFoldersCurrentTask;
            }
            _checkChangesInFoldersCurrentTask = CheckChangesInFoldersAsync();
            await _checkChangesInFoldersCurrentTask;
        }

        // called from longpoll thread when changes = true
        private async Task CheckChangesInFoldersAsync(){
            var folders = _folderSubscriptions.Select(x => x.Key);
            foreach(var folder in folders){
                await CheckChangesInFolder(folder);
            }
        }

        // called from longpoll when changes = true or after adding new folder subscription 
        private async Task CheckChangesInFolder(string dropboxFolderPath){
            // list_folder 
            var listFolderResponse = await new ListFolderRequest(new ListFolderRequestParameters {
                recursive = true,
                include_deleted = true                
            }, _config).ExecuteAsync();

            // process entries
            listFolderResponse.entries.ForEach(entry => ProcessReceivedMetadata(entry));
            
            bool has_more = listFolderResponse.has_more;
            string cursor = listFolderResponse.cursor;
            while(has_more){
                Debug.LogWarning("CheckChangesInFolder: has_more");

                // list_folder/continue
                var listFolderContinueResponse = await new ListFolderContinueRequest(new CursorRequestParameters {
                    cursor = cursor
                }, _config).ExecuteAsync();

                // process entries
                listFolderResponse.entries.ForEach(entry => ProcessReceivedMetadata(entry));
                
                has_more = listFolderContinueResponse.has_more;
                cursor = listFolderContinueResponse.cursor;
            }

            // update _lastCursor for next longpoll
            _lastCursor = cursor;            
        }

        private void ProcessReceivedMetadata(Metadata metadata){
            // detect changes based on local metadata
            // call FileChange events for subscriptions if detected changes

            Debug.Log($"Process received metadata: {metadata}");
            // if(metadata.EntryType == DropboxEntryType.File){
                
            // }
        }


        public void Dispose () {            
            _isDisposed = true;            
        }
    }
    
}