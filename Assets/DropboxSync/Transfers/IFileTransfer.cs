using System.Threading.Tasks;

namespace DBXSync {
    public interface IFileTransfer {        
        string DropboxPath {get;}
        string LocalPath {get;}
        int Progress {get;}
        System.Progress<TransferProgressReport> ProgressCallback {get;}
        TaskCompletionSource<Metadata> CompletionSource {get;}

        System.Threading.Tasks.Task<Metadata> ExecuteAsync();
        void Cancel();
    }
}