using System;
using System.IO;
using System.Net;
using System.Text;
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
        

        public UploadFileTransfer(string localPath, string dropboxTargetPath, IProgress<int> progressCallback, 
                                    TaskCompletionSource<FileMetadata> completionSource, DropboxSyncConfiguration config)
        {
            _config = config;

            _localPath = localPath;
            _dropboxTargetPath = dropboxTargetPath;

            _progressCallback = progressCallback;
            _progress = 0;
            _completionSource = completionSource;            

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<FileMetadata> ExecuteAsync () {
            var cancellationToken = _cancellationTokenSource.Token;

            if(!File.Exists(_localPath)){
                throw new FileNotFoundException($"Uploading file not found: {_localPath}");
            }

            // go to background thread            
            await new WaitForBackgroundThread();

            long fileSize = new FileInfo(_localPath).Length;            

            // send start request
            Debug.LogWarning($"Sending start upload request..."); 
            var startUploadResponse = await new UploadStartRequest(new UploadStartRequestParameters(), _config).ExecuteAsync(new byte[0]);
            string sessionId = startUploadResponse.session_id;      

            Debug.LogWarning($"Starting upload with session id {sessionId}");     

            long chunksUploaded = 0;
            long totalChunks = 1 + fileSize / _config.uploadChunkSizeBytes;
            long totalBytesUploaded = 0;
            
            // uploading chunks is serial (can't be in parallel): https://www.dropboxforum.com/t5/API-Support-Feedback/Using-upload-session-append-v2-is-is-possible-to-upload-chunks/m-p/225947/highlight/true#M12305
            using (FileStream file = new FileStream (_localPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {

                var chunkDataBuffer = new byte[_config.uploadChunkSizeBytes];

                foreach (long chunkIndex in Utils.LongRange (0, totalChunks)) {
                    // read from local file to buffer                    
                    int chunkDataLength = await file.ReadAsync(chunkDataBuffer, 0, (int)_config.uploadChunkSizeBytes);                   

                    var uploadAppendParameters = new UploadAppendRequestParameters(session_id: sessionId, offset: totalBytesUploaded);
                    var uploadAppendRequest = new UploadAppendRequest(uploadAppendParameters, _config);

                    await uploadAppendRequest.ExecuteAsync(chunkDataBuffer.SubArray(0, chunkDataLength), new Progress<int>((chunkUploadProgress) => {
                        // Debug.Log($"Chunk {chunksUploaded} upload progress: {progress}");
                        long currentlyUploadedBytes = totalBytesUploaded + chunkDataLength/100*chunkUploadProgress;
                        ReportProgress((int)(currentlyUploadedBytes * 100 / fileSize));
                    }), cancellationToken);

                    chunksUploaded += 1;
                    totalBytesUploaded += chunkDataLength;                    
                }                
            }

            Debug.LogWarning($"Committing upload...");
            // send finish request            
            var metadata = await new UploadFinishRequest(new UploadFinishRequestParameters(sessionId, totalBytesUploaded, _dropboxTargetPath), _config).ExecuteAsync(new byte[0]);
            
            // return to the Unity thread
            await new WaitForUpdate();

            ReportProgress(100);

            Debug.LogWarning($"Upload done.");

            return metadata;
        }

        public void Cancel() {
            _cancellationTokenSource.Cancel();
        }

        private void ReportProgress(int progress){
            if(progress != _progress){
                _progress = progress;
                _progressCallback.Report (progress);
            }            
        }
    }
}