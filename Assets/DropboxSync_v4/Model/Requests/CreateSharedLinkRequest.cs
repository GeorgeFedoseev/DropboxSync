namespace DBXSync {
    public class CreateSharedLinkRequest : Request<SharedLinkResponse> {
        public CreateSharedLinkRequest(SharedLinkRequestParameters parameters, DropboxSyncConfiguration config)
        : base(Endpoints.SHARED_LINK_WITH_SETTINGS, parameters, config, timeoutMilliseconds: config.lightRequestTimeoutMilliseconds) { }
    }
}
