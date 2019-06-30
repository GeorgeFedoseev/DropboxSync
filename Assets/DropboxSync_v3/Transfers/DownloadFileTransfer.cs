using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

            var transferCancellationToken = _internalCancellationTokenSource.Token;
            
            if(_metadata == null){
                _metadata = (await new GetMetadataRequest (new GetMetadataRequestParameters {
                    path = _dropboxPath
                }, _config).ExecuteAsync ()).GetMetadata ();
            }            

            string tempDownloadPath = Utils.GetDownloadTempFilePath(_localTargetPath, _metadata.content_hash);            

            long fileSize = _metadata.size;
            Metadata latestMetadata = _metadata;

            var speedTracker = new TransferSpeedTracker(_config.speedTrackerSampleSize, TimeSpan.FromMilliseconds(_config.speedTrackerSampleIntervalMilliseconds));  

            Utils.EnsurePathFoldersExist (tempDownloadPath);

            if(fileSize == 0){
                // just create empty file
                File.WriteAllBytes(tempDownloadPath, new byte[0]);
            }else{
                // download chunk by chunk to temp file                
                using (FileStream fileStream = new FileStream (tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {                

                    long chunksDownloaded = 0;
                    long totalChunks = 1 + fileSize / _config.downloadChunkSizeBytes;
                    long totalBytesRead = 0;

                    foreach (long chunkIndex in Utils.LongRange (0, totalChunks)) {

                        var requestParameters = new PathParameters { path = $"rev:{_metadata.rev}"};
                        var parametersJSONString = requestParameters.ToString();

                        // Debug.Log($"{_dropboxPath}: Downloading chunk {chunkIndex}...");

                        // retry loop
                        int failedAttempts = 0;
                        while(true){
                            transferCancellationToken.ThrowIfCancellationRequested();

                            try {

                                using (var client = new HttpClient()){
                                    
                                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.accessToken);
                                    client.DefaultRequestHeaders.Add("Dropbox-API-Arg", parametersJSONString);                                
                                    client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(chunkIndex * _config.downloadChunkSizeBytes,
                                            chunkIndex * _config.downloadChunkSizeBytes + _config.downloadChunkSizeBytes - 1);

                                    
                                    var getResponseCTS = CancellationTokenSource.CreateLinkedTokenSource(transferCancellationToken);
                                    getResponseCTS.CancelAfter(_config.downloadChunkReadTimeoutMilliseconds);
                                    var headersResponse = await client.GetAsync(Endpoints.DOWNLOAD_FILE_ENDPOINT, HttpCompletionOption.ResponseHeadersRead, getResponseCTS.Token);                                                                    

                                    try {
                                        // throw exception if not success status code
                                        headersResponse.EnsureSuccessStatusCode();       
                                    }catch(HttpRequestException ex){
                                        await Utils.RethrowDropboxHttpRequestException(ex, headersResponse, requestParameters, Endpoints.DOWNLOAD_FILE_ENDPOINT);                    
                                    }

                                    var fileMetadataJSONString = headersResponse.Headers.GetValues("Dropbox-API-Result").First();
                                    latestMetadata = JsonUtility.FromJson<Metadata>(fileMetadataJSONString);

                                    fileStream.Seek (chunkIndex * _config.downloadChunkSizeBytes, SeekOrigin.Begin);

                                    using (Stream responseStream = await headersResponse.Content.ReadAsStreamAsync ()) {
                                        
                                        byte[] buffer = new byte[_config.transferBufferSizeBytes];

                                        while(true){
                                            var readToBufferTask = responseStream.ReadAsync (buffer, 0, buffer.Length);
                                            if(await Task.WhenAny(readToBufferTask, Task.Delay(_config.downloadChunkReadTimeoutMilliseconds)).ConfigureAwait(false) == readToBufferTask){
                                                int bytesRead = readToBufferTask.Result;

                                                // exit loop condition
                                                if(bytesRead <= 0){
                                                    break;
                                                }

                                                transferCancellationToken.ThrowIfCancellationRequested();

                                                await fileStream.WriteAsync (buffer, 0, bytesRead).ConfigureAwait(false);                                                
                                                totalBytesRead += bytesRead;

                                                speedTracker.SetBytesCompleted(totalBytesRead);
                                                if(fileSize > 0){
                                                    ReportProgress((int)(totalBytesRead * 100 / fileSize), speedTracker.GetBytesPerSecond());  
                                                }                                            
                                            }else{
                                                // timed-out
                                                // close stream
                                                responseStream.Close();
                                                // throw canceled exception
                                                throw new TimeoutException("Read chunk to buffer timed-out");
                                            }                                        
                                        }                                       
                                    }                               
                                }

                                chunksDownloaded += 1;                 

                                // success - exit retry loop
                                break;

                            }catch(Exception ex){
                                // Debug.Log($"Chunk download exception: {ex}");

                                // dont retry if cancel request
                                if(ex is OperationCanceledException || ex is TaskCanceledException || ex is AggregateException && ((AggregateException)ex).InnerException is TaskCanceledException){                                    
                                    throw new OperationCanceledException();
                                }           

                                failedAttempts += 1;
                                if(failedAttempts <= _config.chunkTransferMaxFailedAttempts){
                                    Debug.LogWarning($"[DropboxSync/DownloadFileTransfer] Failed to download chunk {chunkIndex} of {_dropboxPath}. Retry {failedAttempts}/{_config.chunkTransferMaxFailedAttempts} in {_config.chunkTransferErrorRetryDelaySeconds} seconds...\nException: {ex}");
                                    // wait before attempting again
                                    await Task.Delay(TimeSpan.FromSeconds(_config.chunkTransferErrorRetryDelaySeconds));
                                    continue;                                
                                }else{
                                    // attempts exceeded - exit retry loop
                                    throw ex;
                                }                                
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

            // wait for last progress report to deliver before returning the upload result
            await Task.Delay(1);

            _endDateTime = DateTime.Now;

            return latestMetadata;
        }        

        public void Cancel() {
            // Debug.Log($"[D][DropboxSync/DownloadFileTransfer] Cancel {this}");
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

        public void SetEndDateTime(DateTime dateTime) {
            _endDateTime = dateTime;
        }
    }
}