namespace DBXSync {

    public class UploadAppendRequest : Request<Response> {

        public UploadAppendRequest(UploadAppendRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.UPLOAD_APPEND_ENDPOINT, parameters, config){}

    }

}