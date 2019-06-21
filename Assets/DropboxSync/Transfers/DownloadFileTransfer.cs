using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class DownloadFileTransfer : IFileTransfer {

        public string DropboxPath => _dropboxPath;
        public string LocalPath => _localTargetPath;

        public DateTime StartDateTime => _startDateTime;
        public DateTime EndDateTime => _endDateTime;
        
        public TransferProgressReport Progress => _latestProgressReport;
        public Progress<TransferProgressReport> ProgressCallback => _progressCallback;

        public TaskCompletionSource<Metadata> CompletionSource => _completionSource;
        public CancellationToken CancellationToken => _externalCancellationToken;

        public Metadata Metadata => _metadata;

        private string _dropboxPath;
        private Metadata _metadata = null;
        private string _localTargetPath;
        private DropboxSyncConfiguration _config;        
        private TransferProgressReport _latestProgressReport;
        private Progress<TransferProgressReport> _progressCallback;
        private TaskCompletionSource<Metadata> _completionSource;
        private CancellationTokenSource _internalCancellationTokenSource;
        private CancellationToken _externalCancellationToken;

        private DateTime _startDateTime;
        private DateTime _endDateTime = DateTime.MaxValue;

        public DownloadFileTransfer (string dropboxPath, string localTargetPath, Progress<TransferProgressReport> progressCallback, DropboxSyncConfiguration config, CancellationToken? cancellationToken = null) {            
            _metadata = null;
            _dropboxPath = dropboxPath;
            _InitCommon(localTargetPath, progressCallback, config, cancellationToken);        
        }

        public DownloadFileTransfer (Metadata metadata, string localTargetPath, Progress<TransferProgressReport> progressCallback, DropboxSyncConfiguration config, CancellationToken? cancellationToken = null) {            
            _metadata = metadata;
            _dropboxPath = metadata.path_lower;
            _InitCommon(localTargetPath, progressCallback, config, cancellationToken);
        }

        private void _InitCommon(string localTargetPath, Progress<TransferProgressReport> progressCallback, 
                                   DropboxSyncConfiguration config, CancellationToken? cancellationToken)
        {
            _localTargetPath = localTargetPath;
            _progressCallback = progressCallback;
            _latestProgressReport = new TransferProgressReport(0, 0);
            _completionSource = new TaskCompletionSource<Metadata> ();            
            _config = config;

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
            
            if(_metadata == null){
                _metadata = (await new GetMetadataRequest (new GetMetadataRequestParameters {
                    path = _dropboxPath
                }, _config).ExecuteAsync ()).GetMetadata ();
            }            

            string tempDownloadPath = Utils.GetDownloadTempFilePath(_localTargetPath, _metadata.content_hash);            

            long fileSize = _metadata.size;
            Metadata latestMetadata = null;

            var speedTracker = new TransferSpeedTracker(1000, TimeSpan.FromMilliseconds(1000));  

            // download chunk by chunk to temp file
            Utils.EnsurePathFoldersExist (tempDownloadPath);
            using (FileStream file = new FileStream (tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {

                long chunksDownloaded = 0;
                long totalChunks = 1 + fileSize / _config.downloadChunkSizeBytes;
                long totalBytesRead = 0;
                
                file.SetLength (fileSize); // set the length first

                foreach (long chunkIndex in Utils.LongRange (0, totalChunks)) {

                    var requestParameters = new PathParameters { path = $"rev:{_metadata.rev}"};
                    var parametersJSONString = requestParameters.ToString();

                    Debug.Log($"{_dropboxPath}: Downloading chunk {chunkIndex}...");

                    // retry loop
                    int failedAttempts = 0;
                    while(true){
                        try {

                            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.DOWNLOAD_FILE_ENDPOINT);

                            request.Headers.Set ("Authorization", "Bearer " + _config.accessToken);
                            request.Headers.Set ("Dropbox-API-Arg", parametersJSONString);

                            request.AddRange (chunkIndex * _config.downloadChunkSizeBytes,
                                        chunkIndex * _config.downloadChunkSizeBytes + _config.downloadChunkSizeBytes - 1);

                    
                            using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync ()) {
                                var fileMetadataJSONString = response.Headers["Dropbox-API-Result"];
                                latestMetadata = JsonUtility.FromJson<Metadata>(fileMetadataJSONString);

                                file.Seek (chunkIndex * _config.downloadChunkSizeBytes, SeekOrigin.Begin);

                                Debug.Log($"{_dropboxPath}: Getting response stream for chunk {chunkIndex}...");
                                using (Stream responseStream = response.GetResponseStream ()) {
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;

                                    Debug.Log($"{_dropboxPath}: Got response stream for chunk {chunkIndex}");

                                    while(true){
                                        var readToBufferTask = responseStream.ReadAsync (buffer, 0, buffer.Length);
                                        if (await Task.WhenAny(readToBufferTask, Task.Delay(_config.downloadChunkReadTimeoutMilliseconds)) == readToBufferTask) {
                                            // read completed within timeout
                                            bytesRead = readToBufferTask.Result;

                                            // exit loop condition
                                            if(bytesRead <= 0){
                                                break;
                                            }

                                            cancellationToken.ThrowIfCancellationRequested();

                                            await file.WriteAsync (buffer, 0, bytesRead);
                                            totalBytesRead += bytesRead;

                                            speedTracker.SetBytesCompleted(totalBytesRead);
                                            ReportProgress((int)(totalBytesRead * 100 / fileSize), speedTracker.GetBytesPerSecond());  

                                        } else { 
                                            // timeout
                                            throw new TimeoutException("Download chunk read timeout");
                                        }
                                    }                                   
                                }

                                chunksDownloaded += 1;
                            }                            

                            // success - exit retry loop
                            break;

                        }catch(Exception ex){
                            Debug.Log($"Chunk download exception: {ex}");

                            // dont retry if cancel request
                            if(ex is OperationCanceledException){
                                throw ex;
                            }

                            if(ex is WebException){
                                ex = Utils.DecorateDropboxRequestWebException(ex as WebException, requestParameters, Endpoints.DOWNLOAD_FILE_ENDPOINT);
                            }                            

                            failedAttempts += 1;
                            if(failedAttempts <= _config.chunkTransferMaxFailedAttempts){
                                Debug.LogWarning($"Failed to download chunk {chunkIndex}. Retry {failedAttempts}/{_config.chunkTransferMaxFailedAttempts}\nException: {ex}");
                                // wait before attempting again
                                await new WaitForSeconds(_config.requestErrorRetryDelaySeconds);
                                continue;                                
                            }else{
                                // attempts exceeded - exit retry loop
                                throw ex;
                            }                                
                        }    
                    }

                    Debug.Log($"{_dropboxPath}: Chunk  {chunkIndex} done");                   
                }                
            }
            
            // ensure final folder exists
            Utils.EnsurePathFoldersExist (_localTargetPath);

            // move file to final location (maybe replace old one) 
            if(File.Exists(_localTargetPath)){
                File.Delete(_localTargetPath);
            }

            File.Move(tempDownloadPath, _localTargetPath);

            // report complete progress
            ReportProgress(100, speedTracker.GetBytesPerSecond());

            _endDateTime = DateTime.Now;

            return latestMetadata;
        }        

        public void Cancel() {
            _internalCancellationTokenSource.Cancel();
        }

        private void ReportProgress(int progress, double bytesPerSecond){
            
            if(progress != _latestProgressReport.progress || bytesPerSecond != _latestProgressReport.bytesPerSecondSpeed){                  
                _latestProgressReport = new TransferProgressReport(progress, bytesPerSecond);
                ((IProgress<TransferProgressReport>)_progressCallback).Report(_latestProgressReport);
            }  
        }

        public override string ToString() {
            return $"[DownloadFileTransfer] {_dropboxPath}";
        }
    }
}