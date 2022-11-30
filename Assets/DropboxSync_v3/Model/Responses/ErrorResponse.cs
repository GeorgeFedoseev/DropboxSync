namespace DBXSync {

    [System.Serializable]
    public class ErrorResponse : JSONSerializableObject {
        public string error;
        public string error_description;
    }

}