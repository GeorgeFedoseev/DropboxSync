namespace DBXSync {

    [System.Serializable]
    public class Metadata : JSONSerializableObject  {        
        // https://www.dropbox.com/developers/documentation/http/documentation#files-get_metadata

        public string tag;
        public string name;
        public string id;        
        public string path_lower;
        public string path_display;

    }

}