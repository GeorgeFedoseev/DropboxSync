namespace DBXSync {
    [System.Serializable]
    public class CommitInfo {
        public string path;
        public string mode;
        public bool autorename = false;
        public bool mute = false;
        public bool strict_conflict = false;
    }
}