namespace DBXSync {
    public class DeleteRequest : Request<MetadataResponse> {
        public DeleteRequest(PathParameters parameters, DropboxSyncConfiguration config) 
        : base(Endpoints.DELETE_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds) {}
    }
}