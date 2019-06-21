namespace DBXSync {

    public class ListFolderRequest : Request<ListFolderResponse> {
        public ListFolderRequest(ListFolderRequestParameters parameters, DropboxSyncConfiguration config) 
        : base(Endpoints.LIST_FOLDER_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds) {
        }
    }
}