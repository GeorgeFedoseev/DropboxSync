

public enum DBXFileChangeType {
    None,
    Modified,
    Deleted,
    Added
}

public class DBXFileChange {
    public string path;
    public DBXFileChangeType change;    

    public DBXFileChange(string p, DBXFileChangeType c){
        path = p;
        change = c;
    }
}