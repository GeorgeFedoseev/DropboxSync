namespace DBXSync {

    public class ListFolderContinueRequest : Request<ListFolderResponse> {
        public ListFolderContinueRequest(CursorRequestParameters parameters, DropboxSyncConfiguration config) : 
        base(Endpoints.LIST_FOLDER_CONTINUE_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds) {
        }
    }
}