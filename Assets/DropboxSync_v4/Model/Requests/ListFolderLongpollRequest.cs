namespace DBXSync {

    public class ListFolderLongpollRequest : Request<ListFolderLongpollResponse> {
        public ListFolderLongpollRequest(ListFolderLongpollRequestParameters parameters, DropboxSyncConfiguration config) : 
            base(Endpoints.LIST_FOLDER_LONGPOLL_ENDPOINT, parameters, config, requiresAuthorization: false, timeoutMilliseconds: parameters.timeout*1000 + 1000) {
        }
    }
}