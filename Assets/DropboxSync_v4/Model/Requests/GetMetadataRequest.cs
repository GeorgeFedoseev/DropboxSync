namespace DBXSync {

    public class GetMetadataRequest : Request<GetMetadataResponse> {

        public GetMetadataRequest(GetMetadataRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.METADATA_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds){}

        public GetMetadataRequest(string dropboxPath, DropboxSyncConfiguration config) 
        : base(Endpoints.METADATA_ENDPOINT, new GetMetadataRequestParameters {path = dropboxPath}, config,
                 timeoutMilliseconds: config.lightRequestTimeoutMilliseconds){}

    }

}