namespace DBXSync {

    [System.Serializable]
    public class GetFolderMetadataResponse : GetMetadataResponse {
        public FolderSharingInfo sharing_info;

        public FolderMetadata GetMetadata() {
            return UnityEngine.JsonUtility.FromJson<FolderMetadata>(this.ToString());
        }
    }
}