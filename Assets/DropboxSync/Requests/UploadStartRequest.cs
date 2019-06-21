namespace DBXSync {

    public class UploadStartRequest : Request<UploadStartResponse> {

        public UploadStartRequest(UploadStartRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.UPLOAD_START_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds){}

    }

}