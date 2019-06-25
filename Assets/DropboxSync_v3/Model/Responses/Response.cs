namespace DBXSync {

    [System.Serializable]
    public class Response : JSONSerializableObject {
        public string error_summary;
        public ErrorType error;
    }

}