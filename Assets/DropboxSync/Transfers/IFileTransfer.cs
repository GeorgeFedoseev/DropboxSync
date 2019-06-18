using System.Threading.Tasks;

namespace DBXSync {
    public interface IFileTransfer {        
        string DropboxPath {get;}
        string LocalPath {get;}
        int Progress {get;}
        System.IProgress<TransferProgressReport> ProgressCallback {get;}
        TaskCompletionSource<FileMetadata> CompletionSource {get;}

        System.Threading.Tasks.Task<FileMetadata> ExecuteAsync();
        void Cancel();
    }
}