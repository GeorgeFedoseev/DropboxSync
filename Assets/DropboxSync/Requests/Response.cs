namespace DBXSync {

    [System.Serializable]
    public class Response {
        public string error_summary;
        public DropboxErrorType error;

        public override string ToString() {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }

}