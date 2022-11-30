using System.Collections.Generic;

namespace DBXSync {

    [System.Serializable]
    public class ListFolderResponse : Response {
        public List<Metadata> entries;
        public string cursor;
        public bool has_more;
    }
}