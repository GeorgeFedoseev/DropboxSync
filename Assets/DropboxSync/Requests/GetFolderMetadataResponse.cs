namespace DBXSync {

    [System.Serializable]
    public class GetFolderMetadataResponse : GetMetadataResponse {
        public DropboxFolderSharingInfo sharing_info;
    }
}