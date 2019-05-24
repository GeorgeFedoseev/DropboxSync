namespace DBXSync {

    [System.Serializable]
    public class FileSharingInfo : JSONSerializableObject {
        public string read_only;
        public string parent_shared_folder_id;
        public string modified_by;
    }

}