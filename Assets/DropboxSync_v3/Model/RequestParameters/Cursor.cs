namespace DBXSync {

    [System.Serializable]
    public class Cursor {
        public string session_id;
        public long offset = 0;
    }
}