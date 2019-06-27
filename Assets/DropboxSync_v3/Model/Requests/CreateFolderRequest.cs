namespace DBXSync {
    public class CreateFolderRequest : Request<CreateFolderResponse> {
        public CreateFolderRequest(CreateFolderRequestParameters parameters, DropboxSyncConfiguration config) 
            : base(Endpoints.CREATE_FOLDER_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds){

        }
    }
}