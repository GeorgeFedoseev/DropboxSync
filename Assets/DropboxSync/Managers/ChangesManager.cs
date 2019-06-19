using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DBXSync {

    public class ChangesManager {

        private DropboxSyncConfiguration _config;

        private Thread _backgroundThread;
        private volatile bool _isDisposed = false;

        // keep track of last cursor to longpoll on it for changes in whole account
        private string _lastCursor;

        private Dictionary<string, List<Action<FileChange>>> _folderSubscriptions = new Dictionary<string, List<Action<FileChange>>>();
        private Dictionary<string, string> _folderCursors = new Dictionary<string, string>();

        public ChangesManager (DropboxSyncConfiguration config) {
            _config = config;

            _backgroundThread = new Thread (_backgroudWorker);
            _backgroundThread.IsBackground = true;
            _backgroundThread.Start ();
        }

        private void _backgroudWorker () {
            while (!_isDisposed) {
                // if _lastCursor != null start longpoll request on this cursor
                // if request returned changes = true call CheckChangesInFolders
                // else start long poll again (after backoff timeout if needed)
                Thread.Sleep (100);
            }
        }

        // called from longpoll thread when changes = true
        private void CheckChangesInFolders(){
            var folders = _folderSubscriptions.Select(x => x.Key);
            foreach(var folder in folders){
                CheckChangesInFolder(folder);
            }
        }

        // called from longpoll when changes = true or after adding new folder subscription 
        private async void CheckChangesInFolder(string dropboxFolderPath){
            // list_folder 
            var listFolderResponse = await new ListFolderRequest(new ListFolderRequestParameters {
                recursive = true,
                include_deleted = true                
            }, _config).ExecuteAsync();

            // process entries
            listFolderResponse.entries.ForEach(entry => ProcessListFolderMetadata(entry));
            
            bool has_more = listFolderResponse.has_more;
            string cursor = listFolderResponse.cursor;
            while(has_more){
                // list_folder/continue
                var listFolderContinueResponse = await new ListFolderContinueRequest(new CursorRequestParameters {
                    cursor = cursor
                }, _config).ExecuteAsync();

                // process entries
                listFolderResponse.entries.ForEach(entry => ProcessListFolderMetadata(entry));
                
                has_more = listFolderContinueResponse.has_more;
                cursor = listFolderContinueResponse.cursor;
            }

            // update _lastCursor for next longpoll
            _lastCursor = cursor;
        }

        private void ProcessListFolderMetadata(Metadata metadata){
            // detect changes based on local metadata
            // call FileChange events for subscriptions if detected changes
        }


        public void Dispose () {            
            _isDisposed = true;            
        }
    }
    
}