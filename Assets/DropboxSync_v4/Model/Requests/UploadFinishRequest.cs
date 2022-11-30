namespace DBXSync {

    public class UploadFinishRequest : Request<Metadata> {

        public UploadFinishRequest(UploadFinishRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.UPLOAD_FINISH_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds){}

    }

}