namespace DBXSync {
    public class MoveRequest: Request<MetadataResponse> {
        public MoveRequest(MoveRequestParameters parameters, DropboxSyncConfiguration config)
            :base(Endpoints.MOVE_ENDPOINT, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds) {}
    }
}