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

            long fileSize = new FileInfo(_localPath).Length;            

            // send start request
            var startUploadResponse = await new UploadStartRequest(new UploadStartRequestParameters(), _config).ExecuteAsync();
            string sessionId = startUploadResponse.session_id;      

            Debug.Log($"Starting upload with session id {sessionId}");     

            long chunksUploaded = 0;
            long totalChunks = 1 + fileSize / _config.uploadChunkSizeBytes;
            long totalBytesUploaded = 0;
            
            // uploading chunks is serial (cant be in parallel): https://www.dropboxforum.com/t5/API-Support-Feedback/Using-upload-session-append-v2-is-is-possible-to-upload-chunks/m-p/225947/highlight/true#M12305
            using (FileStream file = new FileStream (_localPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {

                var chunkData = new byte[_config.uploadChunkSizeBytes];

                foreach (long chunkIndex in Utils.LongRange (0, totalChunks)) {

                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.UPLOAD_APPEND_ENDPOINT);

                    // read from local file to buffer                    
                    int chunkDataLength = await file.ReadAsync(chunkData, (int)totalBytesUploaded, (int)_config.uploadChunkSizeBytes);

                    var uploadAppendParameters = new UploadAppendRequestParameters(session_id: sessionId, offset: totalBytesUploaded);
                    var parametersJSONString = uploadAppendParameters.ToString();

                    request.Headers.Set ("Authorization", "Bearer " + _config.accessToken);
                    request.Headers.Set ("Dropbox-API-Arg", parametersJSONString);                    
                    
                    request.Method = "POST";
                    request.ContentType = "application/octet-stream";
                    // request.ContentLength = chunkDataLength;
                    using (Stream postStream = request.GetRequestStream()) {
                        using(var ms = new MemoryStream(chunkData)){
                            ms.SetLength(chunkDataLength);

                            byte[] buffer = new byte[10000];
                            int bytesRead = 0;
                            while((bytesRead = await ms.ReadAsync(buffer, 0, buffer.Length)) != 0){                                
                                // Send the data.
                                await postStream.WriteAsync(buffer, 0, bytesRead);
                                long currentlyUploadedBytes = totalBytesUploaded + ms.Position + 1;
                                ReportProgress((int)(currentlyUploadedBytes * 100 / fileSize));
                            }
                        }
                        
                        postStream.Close();
                    }

                    try {
                        var appendChunkResponse = await request.GetResponseAsync();
                        appendChunkResponse.Dispose();
                    }catch (WebException ex) {
                        Utils.HandleDropboxRequestWebException(ex, uploadAppendParameters, Endpoints.UPLOAD_APPEND_ENDPOINT);
                    }                       

                    chunksUploaded += 1;
                    totalBytesUploaded += chunkDataLength;
                    
                }                
            }

            // send finish request            
            var metadata = await new UploadFinishRequest(new UploadFinishRequestParameters(sessionId, totalBytesUploaded, _dropboxTargetPath), _config).ExecuteAsync();
            
            ReportProgress(100);

            return metadata;
        }

        public void Cancel() {
            _cancellationTokenSource.Cancel();
        }

        private void ReportProgress(int progress){
            _progress = progress;
            _progressCallback.Report (progress);
        }
    }
}