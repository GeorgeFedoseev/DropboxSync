namespace DBXSync {
    public interface IFileTransfer {        
        string DropboxPath {get;}
        string LocalPath {get;}
        int Progress {get;}

        System.Threading.Tasks.Task<FileMetadata> ExecuteAsync(System.IProgress<int> progress);
    }
}