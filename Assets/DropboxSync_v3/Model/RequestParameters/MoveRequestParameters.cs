namespace DBXSync {
    [System.Serializable]
    public class MoveRequestParameters : RequestParameters {
        public string from_path;
        public string to_path;
        public bool allow_shared_folder = false;
        public bool autorename = false;
        public bool allow_ownership_transfer = false;
    }
}