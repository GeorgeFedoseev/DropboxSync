namespace DBXSync {

    public class GetMetadataRequest : Request<GetMetadataResponse> {

        public GetMetadataRequest(GetMetadataRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.METADATA_ENDPOINT, parameters, config){}

    }

}