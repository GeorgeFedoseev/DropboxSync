using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBXSync.Model;
using UnityEngine;

namespace DBXSync {

    public class DBXChunkedDownloader {

        

        public Action<DBXFile> OnSuccess = (localPath) => {};
        public Action<DBXError> OnError = (err) => {};
        public Action<float> OnProgress = (progress) => {};

        private string _dropboxAccessToken;
        private string _dropboxFilePath;
        private string _targetLocalPath;
        private long _chunkSize;
        private int _threadNum;

        private Thread _workerThread;



        public DBXChunkedDownloader(string dropboxPath, string targetLocalPath, string dropboxAccessToken, long chunkSize=10000000, int threadNum=4){
            _dropboxFilePath = dropboxPath;
            _targetLocalPath = targetLocalPath;

            _dropboxAccessToken = dropboxAccessToken;

            _chunkSize = chunkSize;
            _threadNum = threadNum;
        }

        public void Run(){
            if(_workerThread != null){
                throw new Exception("Download was already started before");
            }

            //Debug.Log("ChunkedDownloader: Run");

            _workerThread = new Thread(_DownloadWorker);
            _workerThread.IsBackground = true;            
            _workerThread.Start();
        }

        void _DownloadWorker(){
            EnsurePathFoldersExist(_targetLocalPath);

            var metadata = GetFileMetadata(_dropboxFilePath);
            //Debug.LogWarning("Metadata path: "+metadata.path);
            long fileSize = metadata.filesize;
            //Debug.LogWarning("Metadata file size: "+metadata.filesize.ToString());

            DBXFile latestMetadata = null;
            
            using (FileStream file = new FileStream(_targetLocalPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                file.SetLength(fileSize); // set the length first

                object syncObject = new object(); // synchronize file writes

                long chunksDownloaded = 0;
                long totalChunks = 1 + fileSize / _chunkSize;
                long totalBytesRead = 0;

                //Debug.LogWarning("Total chunks: "+totalChunks.ToString());

                try {

                    Parallel.ForEach(LongRange(0, totalChunks), 
                            new ParallelOptions() { MaxDegreeOfParallelism = _threadNum }, (start) =>
                    {                        
                        

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Const.DOWNLOAD_FILE_ENDPOINT);                                            

                        var jsonParameters = UnityEngine.JsonUtility.ToJson(new DropboxDownloadFileRequestParams(_dropboxFilePath));

                        request.Headers.Set("Authorization", "Bearer "+_dropboxAccessToken);					
                        request.Headers.Set("Dropbox-API-Arg", jsonParameters);

                        request.AddRange(start * _chunkSize, start * _chunkSize + _chunkSize - 1);

                        //Debug.LogWarning("Downloading chunk "+start.ToString());

                        
                        
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                            
                            lock (syncObject)
                            {

                                var jsonMetadata = response.Headers["Dropbox-API-Result"];
                                latestMetadata = ParseFileMetadataFromJson(jsonMetadata);
                                
                                file.Seek(start * _chunkSize, SeekOrigin.Begin);

                                using(Stream responseStream = response.GetResponseStream()){
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;
                                    while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        file.Write(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;
                                        OnProgress((float)totalBytesRead/fileSize);
                                    }
                                }
                                
                                chunksDownloaded += 1;
                            }
                        }
                    });

                    //Debug.LogWarning("ChunkedDownloader: Finished Parallel.ForEach");

                }catch(Exception ex){
                    OnError(new DBXError(ex.Message, DBXErrorType.NetworkProblem));
                    return;
                }                
            }
            
            //Debug.LogWarning("ChunkedDownloader: Success");
            OnSuccess(latestMetadata);
        }

        private DBXFile GetFileMetadata(string _dropboxFilePath){
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Const.METADATA_ENDPOINT);                    
            // request.KeepAlive = false;
            // request.Proxy = null;                
            // request.Pipelined = true;

            request.ContentType = "application/json";            
            request.Headers.Set("Authorization", "Bearer "+_dropboxAccessToken);					

            var jsonParameters = UnityEngine.JsonUtility.ToJson(new DropboxGetMetadataRequestParams(_dropboxFilePath));
            // request.Headers.Set("Dropbox-API-Arg", jsonParameters);
            
            request.Method = "POST";
            var data = Encoding.UTF8.GetBytes(jsonParameters);
            request.ContentLength = data.Length;


            Stream dataStream = request.GetRequestStream ();  
            dataStream.Write(data, 0, data.Length);
            dataStream.Close();
            
            
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()){   
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8)){
                    var jsonStr = reader.ReadToEnd();

                    //Debug.LogWarning("Received metadata: "+jsonStr);          
                    return ParseFileMetadataFromJson(jsonStr);
                }                
            }
        }

        private DBXFile ParseFileMetadataFromJson(string jsonStr){
            var dict = JSON.FromJson<Dictionary<string, object>>(jsonStr);
            return DBXFile.FromDropboxDictionary(dict);
        }

        // EVENTS
        private void OnFinished(){
            //Debug.Log("ChunkedDownloader: OnFinished");

            _workerThread.Abort();
            _workerThread = null;
        }


        private static void EnsurePathFoldersExist(string path){
            var dirPath = Path.GetDirectoryName(path);				
			Directory.CreateDirectory(dirPath);
        }

        public IEnumerable<long> LongRange(long start, long count){
            var limit = start + count;

            while (start < limit)
            {
                yield return start;
                start++;
            }
        }

    }
}