namespace DBXSync {

    public class GetFileMetadataRequest : Request<GetFileMetadataResponse> {

        public GetFileMetadataRequest(GetMetadataRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.METADATA_ENDPOINT, parameters, config){}

    }

}