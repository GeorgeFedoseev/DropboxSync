namespace DBXSync {

    [System.Serializable]
    public class GetFileMetadataResponse : GetMetadataResponse {
        public DropboxFileSharingInfo sharing_info;
        public bool is_downloadable;
        public bool has_explicit_shared_members;
        public string content_hash;
    }
}