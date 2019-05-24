namespace DBXSync {

    public class FileMetadata : Metadata {
        
        public FileSharingInfo sharing_info;
        public bool is_downloadable;
        public bool has_explicit_shared_members;
        public string content_hash;
    }
}