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
            Debug.Log($"Started upload session with id {sessionId}");
            
            // uploading chunks is serial (cant be in parallel): https://www.dropboxforum.com/t5/API-Support-Feedback/Using-upload-session-append-v2-is-is-possible-to-upload-chunks/m-p/225947/highlight/true#M12305
            using (FileStream file = new FileStream (_localPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                
                long chunksUploaded = 0;
                long totalChunks = 1 + fileSize / _config.uploadChunkSizeBytes;
                long totalBytesUploaded = 0;

                var chunkData = new byte[_config.uploadChunkSizeBytes];

                foreach (long start in Utils.LongRange (0, totalChunks)) {
                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.UPLOAD_APPEND_ENDPOINT);

                    long uploadOffset = start * _config.uploadChunkSizeBytes;                    

                    // read from local file to buffer                    
                    int chunkDataLength = await file.ReadAsync(chunkData, (int)uploadOffset, (int)_config.uploadChunkSizeBytes);

                    var parametersJSONString = 
                        new UploadAppendRequestParameters(session_id: sessionId, offset:uploadOffset).ToString();

                    request.Headers.Set ("Authorization", "Bearer " + _config.accessToken);
                    request.Headers.Set ("Dropbox-API-Arg", parametersJSONString);                    
                    
                    request.Method = "POST";
                    request.ContentType = "application/octet-stream";
                    request.ContentLength = chunkDataLength;
                    using (Stream postStream = request.GetRequestStream()) {
                        // Send the data.
                        await postStream.WriteAsync(chunkData, 0, chunkDataLength);
                        postStream.Close();
                    }

                    using(WebResponse response = await request.GetResponseAsync()) {
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8)) {
                            string responseText = reader.ReadToEnd();
                            Debug.Log($"Upload append response: {responseText}");
                        }                        
                    }                                        

                    chunksUploaded += 1;
                    totalBytesUploaded += chunkDataLength;
                    ReportProgress((int)(totalBytesUploaded * 100 / fileSize));
                }                
            }

            // send finish request            
            var metadata = await new UploadFinishRequest(new UploadFinishRequestParameters(sessionId, _dropboxTargetPath), _config).ExecuteAsync();
            
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