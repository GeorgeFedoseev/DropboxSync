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

        public DateTime StartDateTime => _startDateTime;
        public DateTime EndDateTime => _endDateTime;
        
        public TransferProgressReport Progress => _latestProgressReport;
        public Progress<TransferProgressReport> ProgressCallback => _progressCallback;

        public TaskCompletionSource<Metadata> CompletionSource => _completionSource;
        public CancellationToken CancellationToken => _externalCancellationToken;

        private string _dropboxTargetPath;        
        private string _localPath;
        private DropboxSyncConfiguration _config;        
        private TransferProgressReport _latestProgressReport;
        private Progress<TransferProgressReport> _progressCallback;
        private TaskCompletionSource<Metadata> _completionSource;
        private CancellationTokenSource _internalCancellationTokenSource;
        private CancellationToken _externalCancellationToken;

        private DateTime _startDateTime;
        private DateTime _endDateTime = DateTime.MaxValue;
        

        public UploadFileTransfer(string localPath, string dropboxTargetPath, Progress<TransferProgressReport> progressCallback, 
                                     DropboxSyncConfiguration config, CancellationToken? cancellationToken = null)
        {
            _config = config;

            _localPath = localPath;
            _dropboxTargetPath = dropboxTargetPath;

            _progressCallback = progressCallback;            
            _latestProgressReport = new TransferProgressReport(0, 0);
            _completionSource = new TaskCompletionSource<Metadata> ();            

            _internalCancellationTokenSource = new CancellationTokenSource();
            // register external cancellation token
            if(cancellationToken.HasValue){
                _externalCancellationToken = cancellationToken.Value;
                cancellationToken.Value.Register(Cancel);
            }
        }

        public async Task<Metadata> ExecuteAsync () {
            _startDateTime = DateTime.Now;

            var cancellationToken = _internalCancellationTokenSource.Token;

            if(!File.Exists(_localPath)){
                throw new FileNotFoundException($"Uploading file not found: {_localPath}");
            }

            var speedTracker = new TransferSpeedTracker(_config.speedTrackerSampleSize, TimeSpan.FromMilliseconds(_config.speedTrackerSampleIntervalMilliseconds));

            long fileSize = new FileInfo(_localPath).Length;            

            // send start request
            // Debug.LogWarning($"Sending start upload request..."); 
            var startUploadResponse = await new UploadStartRequest(new UploadStartRequestParameters(), _config).ExecuteAsync(new byte[0]);
            string sessionId = startUploadResponse.session_id;      

            // Debug.LogWarning($"Starting upload with session id {sessionId}");     

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
                    
                    // retry loop
                    int failedAttempts = 0;
                    while(true){
                        try {                           
                            await uploadAppendRequest.ExecuteAsync(chunkDataBuffer.SubArray(0, chunkDataLength), uploadProgress: new Progress<int>((chunkUploadProgress) => {
                                // Debug.Log($"Chunk {chunksUploaded} upload progress: {progress}");
                                long currentlyUploadedBytes = totalBytesUploaded + chunkDataLength/100*chunkUploadProgress;
                                speedTracker.SetBytesCompleted(currentlyUploadedBytes);
                                if(fileSize > 0){
                                    ReportProgress(Mathf.Clamp((int)(currentlyUploadedBytes * 100 / fileSize), 0, 100), speedTracker.GetBytesPerSecond());
                                }                                
                            }), cancellationToken: cancellationToken);

                            // success - exit retry loop
                            break;
                        }catch(Exception ex){
                            // dont retry if cancel request
                            if(ex is OperationCanceledException || ex is TaskCanceledException || ex is AggregateException && ((AggregateException)ex).InnerException is TaskCanceledException){
                                throw new OperationCanceledException();
                            }

                            failedAttempts += 1;
                            if(failedAttempts <= _config.chunkTransferMaxFailedAttempts){
                                Debug.LogWarning($"[DropboxSync/UploadFileTransfer] Failed to upload chunk {chunkIndex} to {_dropboxTargetPath}. Retry {failedAttempts}/{_config.chunkTransferMaxFailedAttempts} in {_config.chunkTransferErrorRetryDelaySeconds} seconds...\nException: {ex}");
                                // wait before attempting again
                                await Task.Delay(TimeSpan.FromSeconds(_config.chunkTransferErrorRetryDelaySeconds));
                                continue;                                
                            }else{
                                // attempts exceeded - exit retry loop
                                throw ex;
                            }
                        }
                    }                    

                    chunksUploaded += 1;
                    totalBytesUploaded += chunkDataLength;                    
                }                
            }

            // Debug.LogWarning($"Committing upload...");
            // send finish request            
            var metadata = await new UploadFinishRequest(new UploadFinishRequestParameters(sessionId, totalBytesUploaded, _dropboxTargetPath), _config).ExecuteAsync(new byte[0]);
            
            // report complete progress
            ReportProgress(100, speedTracker.GetBytesPerSecond());

            // wait for last progress report to deliver before returning the upload result
            await Task.Delay(1);

            _endDateTime = DateTime.Now;

            // Debug.LogWarning($"Upload done.");

            return metadata;
        }

        public void Cancel() {
            _internalCancellationTokenSource.Cancel();
        }

        private void ReportProgress(int progress, double bytesPerSecond){            
            
            if(progress != _latestProgressReport.progress || bytesPerSecond != _latestProgressReport.bytesPerSecondSpeed){
                _latestProgressReport = new TransferProgressReport(progress, bytesPerSecond);
                ((IProgress<TransferProgressReport>)_progressCallback).Report (_latestProgressReport);
            }                                
        }

        public override string ToString() {
            return $"[UploadFileTransfer] {_dropboxTargetPath}";
        }

        public void SetEndDateTime(DateTime dateTime) {
            _endDateTime = dateTime;
        }
    }
}