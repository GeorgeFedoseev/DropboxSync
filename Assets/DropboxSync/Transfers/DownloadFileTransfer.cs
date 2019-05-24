using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class DownloadFileTransfer : IFileTransfer {

        public string DropboxPath =>
            throw new System.NotImplementedException ();

        public string LocalPath =>
            throw new System.NotImplementedException ();

        public int Progress =>
            throw new System.NotImplementedException ();

        private string _dropboxPath;
        private string _localTargetPath;
        private DropboxSyncConfiguration _config;

        public DownloadFileTransfer (string dropboxPath, string localTargetPath, DropboxSyncConfiguration config) {
            // TODO: validate paths
            _dropboxPath = dropboxPath;
            _localTargetPath = localTargetPath;
            _config = config;

        }

        public async Task<FileMetadata> ExecuteAsync (IProgress<int> progress) {
            
            Utils.EnsurePathFoldersExist (_localTargetPath);

            var metadata = (await new GetFileMetadataRequest (new GetMetadataRequestParameters {
                path = _dropboxPath
            }, _config).ExecuteAsync ()).GetMetadata ();

            long fileSize = metadata.size;

            FileMetadata latestMetadata = null;

            using (FileStream file = new FileStream (_localTargetPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {
                file.SetLength (fileSize); // set the length first

                object syncObject = new object (); // synchronize file writes

                long chunksDownloaded = 0;
                long totalChunks = 1 + fileSize / _config.downloadChunkSizeBytes;
                long totalBytesRead = 0;

                //Debug.LogWarning("Total chunks: "+totalChunks.ToString());                

                Parallel.ForEach (Utils.LongRange (0, totalChunks),
                    new ParallelOptions () { MaxDegreeOfParallelism = _config.downloadThreadNum }, (start) => {

                        HttpWebRequest request = (HttpWebRequest) WebRequest.Create (Endpoints.DOWNLOAD_FILE_ENDPOINT);

                        var parametersJSONString = new PathParameters { path = _dropboxPath}.ToString();

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
                                        progress.Report ((int)(totalBytesRead * 100 / fileSize));
                                    }
                                }

                                chunksDownloaded += 1;
                            }
                        }
                });                
            }

            return latestMetadata;
        }
    }
}