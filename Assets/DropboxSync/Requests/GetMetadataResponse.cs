namespace DBXSync {

    [System.Serializable]
    public class GetMetadataResponse : Response {        
        // https://www.dropbox.com/developers/documentation/http/documentation#files-get_metadata

        public string tag;
        public string name;
        public string id;
        public string client_modified;
        public string server_modified;
        public string rev;
        public long size;
        public string path_lower;
        public string path_display;

    }

}