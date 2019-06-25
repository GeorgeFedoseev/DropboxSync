using UnityEngine;

namespace DBXSync {

    [System.Serializable]
    public class GetMetadataResponse : Response {        
        // https://www.dropbox.com/developers/documentation/http/documentation#files-get_metadata

        // common file and folder
        public string tag;
        public string name;
        public string id;        
        public string path_lower;
        public string path_display;
        public SharingInfo sharing_info;

        // file
        public string client_modified;
        public string server_modified;
        public string rev;
        public long size;        
        public bool is_downloadable;
        public bool has_explicit_shared_members;
        public string content_hash;        

        public Metadata GetMetadata(){
            return JsonUtility.FromJson<Metadata>(this.ToString());
        }

    }

}