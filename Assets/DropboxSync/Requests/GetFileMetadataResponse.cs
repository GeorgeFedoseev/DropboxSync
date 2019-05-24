namespace DBXSync {

    [System.Serializable]
    public class GetFileMetadataResponse : GetMetadataResponse {
        public FileSharingInfo sharing_info;
        public bool is_downloadable;
        public bool has_explicit_shared_members;
        public string content_hash;

        public FileMetadata GetMetadata() {
            return UnityEngine.JsonUtility.FromJson<FileMetadata>(this.ToString());
        }
    }
}