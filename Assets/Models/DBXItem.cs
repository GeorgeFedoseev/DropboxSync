using System;

namespace DropboxSync.Model {
    
    [Serializable]
    public enum DBXItemType {
        File,
        Folder
    }

    [Serializable]
    public class DBXItem {
        public string id;
        public string name;
        public DBXItemType type;
        public string path;        
    }


}