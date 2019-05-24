namespace DBXSync {

    [System.Serializable]
    public class FolderSharingInfo {
        public string read_only;
        public string parent_shared_folder_id;
        public string modified_by;
        public bool traverse_only;
        public bool no_access;
    }

}