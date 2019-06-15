using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class UploadFileTransfer : IFileTransfer {

        public string DropboxPath => _dropboxTargetPath;
        public string LocalPath => _localPath;
        public int Progress => _progress; 
        public IProgress<int> ProgressCallback => _progressCallback;
        public TaskCompletionSource<FileMetadata> CompletionSource => _completionSource;

        private string _dropboxTargetPath;        
        private string _localPath;
        private DropboxSyncConfiguration _config;
        private int _progress;
        private IProgress<int> _progressCallback;
        private TaskCompletionSource<FileMetadata> _completionSource;
        private CancellationTokenSource _cancellationTokenSource;
        

        public UploadFileTransfer(){
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<FileMetadata> ExecuteAsync () {
            var cancellationToken = _cancellationTokenSource.Token;

            // send start request
            
            // upload in Parallel.ForEach

            // send finish request
            
            throw new NotImplementedException();
        }

        public void Cancel() {
            _cancellationTokenSource.Cancel();
        }
    }
}