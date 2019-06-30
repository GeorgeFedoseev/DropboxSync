using System;
using System.Threading;
using System.Threading.Tasks;

namespace DBXSync {
    public interface IFileTransfer {        
        string DropboxPath {get;}
        string LocalPath {get;}

        DateTime StartDateTime {get;}
        DateTime EndDateTime {get;}

        TransferProgressReport Progress {get;}
        System.Progress<TransferProgressReport> ProgressCallback {get;}
        
        TaskCompletionSource<Metadata> CompletionSource {get;}
        CancellationToken CancellationToken {get;}

        System.Threading.Tasks.Task<Metadata> ExecuteAsync();
        void Cancel();
        void SetEndDateTime(DateTime dateTime);
    }
}