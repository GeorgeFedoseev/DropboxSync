namespace DropboxSync.Model {

    public enum DBXFileChangeType {
        None,
        Modified,
        Deleted,
        Added
    }

    public class DBXFileChange {
        public DBXFile file;
        public DBXFileChangeType change;    
        

        public DBXFileChange(DBXFile f, DBXFileChangeType c){
            file = f;
            change = c;        
        }
    }
}