namespace DBXSync {
    [System.Serializable]
    public class SharedLinkSettingsParameters : RequestParameters {
        public string audience = "public";
        public string access = "viewer";
        public string requested_visibility = "public";
        public bool allow_download = true;
    }
}
