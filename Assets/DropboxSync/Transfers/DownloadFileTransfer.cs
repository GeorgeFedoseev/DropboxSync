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
            Metadata latestMetadata = null;

            var speedTracker = new TransferSpeedTracker(1000, TimeSpan.FromMilliseconds(1000));  

            // download chunk by chunk to temp file
            Utils.EnsurePathFoldersExist (tempDownloadPath);
            using (FileStream fileStream = new FileStream (tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {

                long chunksDownloaded = 0;
                long totalChunks = 1 + fileSize / _config.downloadChunkSizeBytes;
                long totalBytesRead = 0;
                
                fileStream.SetLength (fileSize); // set the length first

                foreach (long chunkIndex in Utils.LongRange (0, totalChunks)) {

                    var requestParameters = new PathParameters { path = $"rev:{_metadata.rev}"};
                    var parametersJSONString = requestParameters.ToString();

                    Debug.Log($"{_dropboxPath}: Downloading chunk {chunkIndex}...");

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
                                var response = await client.GetAsync(Endpoints.DOWNLOAD_FILE_ENDPOINT, HttpCompletionOption.ResponseHeadersRead, getResponseCTS.Token);                                                                    

                                var fileMetadataJSONString = response.Headers.GetValues("Dropbox-API-Result").First();
                                latestMetadata = JsonUtility.FromJson<Metadata>(fileMetadataJSONString);

                                fileStream.Seek (chunkIndex * _config.downloadChunkSizeBytes, SeekOrigin.Begin);

                                using (Stream responseStream = await response.Content.ReadAsStreamAsync ()) {
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;

                                    

                                    while(true){
                                        var readToBufferTask = responseStream.ReadAsync (buffer, 0, buffer.Length);
                                        if(await Task.WhenAny(readToBufferTask, Task.Delay(_config.downloadChunkReadTimeoutMilliseconds)) == readToBufferTask){
                                            bytesRead = readToBufferTask.Result;

                                            // exit loop condition
                                            if(bytesRead <= 0){
                                                break;
                                            }

                                            transferCancellationToken.ThrowIfCancellationRequested();

                                            await fileStream.WriteAsync (buffer, 0, bytesRead);
                                            totalBytesRead += bytesRead;

                                            speedTracker.SetBytesCompleted(totalBytesRead);
                                            ReportProgress((int)(totalBytesRead * 100 / fileSize), speedTracker.GetBytesPerSecond());  
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

                            if(ex is WebException){
                                ex = Utils.DecorateDropboxRequestWebException(ex as WebException, requestParameters, Endpoints.DOWNLOAD_FILE_ENDPOINT);
                            }                            

                            failedAttempts += 1;
                            if(failedAttempts <= _config.chunkTransferMaxFailedAttempts){
                                Debug.LogWarning($"Failed to download chunk {chunkIndex}. Retry {failedAttempts}/{_config.chunkTransferMaxFailedAttempts} ({_dropboxPath})\nException: {ex}");
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