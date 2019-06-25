namespace DBXSync {

    [System.Serializable]
    public class ListFolderRequestParameters : RequestParameters {
        public string path;
        public bool recursive = false;
        public bool include_media_info = false;
        public bool include_deleted = false;
        public bool include_has_explicit_shared_members = false;
        public bool include_mounted_folders = true;
        public bool include_non_downloadable_files = true;
    }
}