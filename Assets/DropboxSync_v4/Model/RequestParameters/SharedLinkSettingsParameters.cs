namespace DBXSync {

    public struct LinkAudienceParam {
        public const string PUBLIC = "public";
        public const string TEAM = "team";
        public const string NO_ONE = "no_one";

    }

    public struct RequestedLinkAccessLevelParam {
        public const string VIEWER = "viewer";
        public const string EDITOR = "editor";
        public const string MAX = "max";
        public const string DEFAULT = "default";

    }

    [System.Serializable]
    public class SharedLinkSettingsParameters : RequestParameters {
        public string audience = LinkAudienceParam.PUBLIC;
        public string access = RequestedLinkAccessLevelParam.VIEWER;
        public bool allow_download = true;
    }
}
