using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class DownloadFileTransfer : IFileTransfer {

        public string DropboxPath => _dropboxPath;
        public string LocalPath => _localTargetPath;
        public int Progress => _progress; 
        public IProgress<int> ProgressCallback => _progressCallback;
        public TaskCompletionSource<FileMetadata> CompletionSource => _completionSource;

        private string _dropboxPath;
        private FileMetadata _metadata = null;
        private string _localTargetPath;
        private DropboxSyncConfiguration _config;
        private int _progress;
        private IProgress<int> _progressCallback;
        private TaskCompletionSource<FileMetadata> _completionSource;

        public DownloadFileTransfer (string dropboxPath, string localTargetPath, IProgress<int> progressCallback, 
                                    TaskCompletionSource<FileMetadata> completionSource, DropboxSyncConfiguration config) {
            // TODO: validate paths
            _metadata = null;
            _dropboxPath = dropboxPath;
            _localTargetPath = localTargetPath;
            _progressCallback = progressCallback;
            _completionSource = completionSource;
            _config = config;
        }

        public DownloadFileTransfer (FileMetadata metadata, string localTargetPath, IProgress<int> progressCallback, 
                                    TaskCompletionSource<FileMetadata> completionSource, DropboxSyncConfiguration config) {
            // TODO: validate paths
            _metadata = metadata;
            _dropboxPath = metadata.path_lower;
            _localTargetPath = localTargetPath;
            _progressCallback = progressCallback;
            _completionSource = completionSource;
            _config = config;
        }

        public async Task<FileMetadata> ExecuteAsync () {
            
            if(_metadata == null){
                _metadata = (await new GetFileMetadataRequest (new GetMetadataRequestParameters {
                    path = _dropboxPath
                }, _config).ExecuteAsync ()).GetMetadata ();
            }            

            string tempDownloadPath = Utils.GetDownloadTempFilePath(_localTargetPath, _metadata.content_hash);            

            long fileSize = _metadata.size;
            FileMetadata latestMetadata = null;

            // go to background thread            
            await new WaitForBackgroundThread();

            Utils.EnsurePathFoldersExist (tempDownloadPath);
            using (FileStream file = new FileStream (tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {
                file.SetLength (fileSize); // set the length first

                object syncObject = new object (); // synchronize file writes

                long chunksDownloaded = 0;
                long totalChunks = 1 + fileSize / _config.downloadChunkSizeBytes;
                long totalBytesRead = 0;

                //Debug.LogWarning("Total chunks: "+totalChunks.ToString());                

                Parallel.ForEach (Utils.LongRange (0, totalChunks),
                    new ParallelOptions () { MaxDegreeOfParallelism = _config.downloadChunckedThreadNum }, (start) => {

                        HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.DOWNLOAD_FILE_ENDPOINT);

                        var parametersJSONString = new PathParameters { path = $"rev:{_metadata.rev}"}.ToString();

                        request.Headers.Set ("Authorization", "Bearer " + _config.accessToken);
                        request.Headers.Set ("Dropbox-API-Arg", parametersJSONString);

                        request.AddRange (start * _config.downloadChunkSizeBytes,
                                            start * _config.downloadChunkSizeBytes + _config.downloadChunkSizeBytes - 1);

                        //Debug.LogWarning("Downloading chunk "+start.ToString());

                        using (HttpWebResponse response = (HttpWebResponse) request.GetResponse ()) {

                            lock (syncObject) {

                                var fileMetadataJSONString = response.Headers["Dropbox-API-Result"];
                                latestMetadata = JsonUtility.FromJson<FileMetadata>(fileMetadataJSONString);

                                file.Seek (start * _config.downloadChunkSizeBytes, SeekOrigin.Begin);

                                using (Stream responseStream = response.GetResponseStream ()) {
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;
                                    while ((bytesRead = responseStream.Read (buffer, 0, buffer.Length)) > 0) {
                                        file.Write (buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;
                                        _progress = (int)(totalBytesRead * 100 / fileSize);
                                        _progressCallback.Report (_progress);
                                    }
                                }

                                chunksDownloaded += 1;
                            }
                        }
                });                
            }
            

            // TODO: 
            // ensure final folder exists
            Utils.EnsurePathFoldersExist (_localTargetPath);
            // move file to final location (maybe replace old one) 
            if(File.Exists(_localTargetPath)){
                File.Delete(_localTargetPath);
            }
            File.Move(tempDownloadPath, _localTargetPath);


            // return to the Unity thread
            await new WaitForUpdate();

            return latestMetadata;
        }
    }
}