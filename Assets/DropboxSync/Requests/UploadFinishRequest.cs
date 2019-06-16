namespace DBXSync {

    public class UploadFinishRequest : Request<FileMetadata> {

        public UploadFinishRequest(UploadFinishRequestParameters parameters, DropboxSyncConfiguration config) 
                : base(Endpoints.UPLOAD_FINISH_ENDPOINT, parameters, config){}

    }

}