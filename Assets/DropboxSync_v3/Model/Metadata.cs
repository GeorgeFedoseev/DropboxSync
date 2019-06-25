namespace DBXSync {

    [System.Serializable]
    public class Metadata : JSONSerializableObject  {        
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

        public DropboxEntryType EntryType {
            get {
                switch(tag){
                    case "file":
                        return DropboxEntryType.File;
                    case "folder":
                        return DropboxEntryType.Folder;
                    case "deleted":
                        return DropboxEntryType.Deleted;
                    default:
                        throw new DropboxAPIException($"Unknown tag: {tag}");
                }
            }
        }

        public bool IsFile => EntryType == DropboxEntryType.File;
        public bool IsFolder => EntryType == DropboxEntryType.Folder;

    }

}