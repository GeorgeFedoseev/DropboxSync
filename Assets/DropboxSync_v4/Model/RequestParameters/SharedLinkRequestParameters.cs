namespace DBXSync {
    [System.Serializable]
    public class SharedLinkRequestParameters : RequestParameters {
        public string path;
        public SharedLinkSettingsParameters settings;
    }
}
