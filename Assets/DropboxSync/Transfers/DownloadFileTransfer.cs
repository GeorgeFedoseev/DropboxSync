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
        public int Progress => _progress; 
        public double BytesPerSecond => _bytesPerSecond;
        public IProgress<TransferProgressReport> ProgressCallback => _progressCallback;
        public TaskCompletionSource<Metadata> CompletionSource => _completionSource;

        private string _dropboxPath;
        private Metadata _metadata = null;
        private string _localTargetPath;
        private DropboxSyncConfiguration _config;
        private int _progress;
        private double _bytesPerSecond;
        private IProgress<TransferProgressReport> _progressCallback;
        private TaskCompletionSource<Metadata> _completionSource;
        private CancellationTokenSource _cancellationTokenSource;

        public DownloadFileTransfer (string dropboxPath, string localTargetPath, IProgress<TransferProgressReport> progressCallback, 
                                    TaskCompletionSource<Metadata> completionSource, DropboxSyncConfiguration config) {            
            _metadata = null;
            _dropboxPath = dropboxPath;
            _localTargetPath = localTargetPath;
            _progressCallback = progressCallback;
            _progress = 0;
            _completionSource = completionSource;            
            _config = config;

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public DownloadFileTransfer (Metadata metadata, string localTargetPath, IProgress<TransferProgressReport> progressCallback, 
                                    TaskCompletionSource<Metadata> completionSource, DropboxSyncConfiguration config) {            
            _metadata = metadata;
            _dropboxPath = metadata.path_lower;
            _localTargetPath = localTargetPath;
            _progressCallback = progressCallback;
            _completionSource = completionSource;            
            _config = config;

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task<Metadata> ExecuteAsync () {
            var cancellationToken = _cancellationTokenSource.Token;
            
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

                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.DOWNLOAD_FILE_ENDPOINT);

                    var requestParameters = new PathParameters { path = $"rev:{_metadata.rev}"};
                    var parametersJSONString = requestParameters.ToString();

                    request.Headers.Set ("Authorization", "Bearer " + _config.accessToken);
                    request.Headers.Set ("Dropbox-API-Arg", parametersJSONString);

                    request.AddRange (chunkIndex * _config.downloadChunkSizeBytes,
                                        chunkIndex * _config.downloadChunkSizeBytes + _config.downloadChunkSizeBytes - 1);

                    //  Debug.LogWarning($"Downloading chunk {chunkIndex}...");
                    // retry loop
                    int failedAttempts = 0;
                    while(true){
                        try {
                            using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync ()) {
                                var fileMetadataJSONString = response.Headers["Dropbox-API-Result"];
                                latestMetadata = JsonUtility.FromJson<Metadata>(fileMetadataJSONString);

                                file.Seek (chunkIndex * _config.downloadChunkSizeBytes, SeekOrigin.Begin);

                                using (Stream responseStream = response.GetResponseStream ()) {
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;
                                    while ((bytesRead = await responseStream.ReadAsync (buffer, 0, buffer.Length)) > 0) {
                                        cancellationToken.ThrowIfCancellationRequested();

                                        await file.WriteAsync (buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;

                                        speedTracker.SetBytesCompleted(totalBytesRead);
                                        ReportProgress((int)(totalBytesRead * 100 / fileSize), speedTracker.GetBytesPerSecond());                                      
                                    }
                                }

                                chunksDownloaded += 1;
                            }                            

                            // success - exit retry loop
                            break;

                        }catch(Exception ex){
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

            return latestMetadata;
        }        

        public void Cancel() {
            _cancellationTokenSource.Cancel();
        }

        private void ReportProgress(int progress, double bytesPerSecond){
            if(progress != _progress || bytesPerSecond != _bytesPerSecond){
                _progress = progress;
                _bytesPerSecond = bytesPerSecond;                
                _progressCallback.Report (new TransferProgressReport(_progress, bytesPerSecond));    
            }  
        }
    }
}