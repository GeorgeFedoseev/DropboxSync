
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Threading;
using System.Net.Http;

namespace DBXSync {

    public class Request<RESP_T> {

        private string _endpoint;
        private RequestParameters _parameters;
        private DropboxSyncConfiguration _config;
        private bool _requiresAuthorization;
        private int _timeoutMilliseconds = int.MaxValue;


        public Request(string endpoint, RequestParameters parameters, DropboxSyncConfiguration config, bool requiresAuthorization = true, int timeoutMilliseconds = int.MaxValue) {
            _endpoint = endpoint;
            _parameters = parameters;
            _config = config;
            _requiresAuthorization = requiresAuthorization;
            _timeoutMilliseconds = timeoutMilliseconds;
        }      


        public async Task<RESP_T> ExecuteAsync(byte[] payload = null, IProgress<int> uploadProgress = null, IProgress<int> downloadProgress = null, CancellationToken? cancellationToken = null, int timeoutMilliseconds = int.MaxValue){

            using (var client = new HttpClient()){
                
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpoint);

                var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);

                // add auth parameters if needed
                if(_requiresAuthorization) {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.accessToken);
                }

                System.Net.Http.Headers.MediaTypeHeaderValue contentType;

                if(payload != null){
                    // add parameters in header
                    requestMessage.Headers.Add ("Dropbox-API-Arg", parametersJSONString);  
                    contentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");                 
                }else{
                    // add parameters in payload
                    payload = Encoding.Default.GetBytes(parametersJSONString);
                    contentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                }

                
                var streamContent = new ProgressableStreamContent(new MemoryStream(payload), async (sourceStream, uploadStream) => {                    
                    // write to uploadStream buffered
                    long totalBytesSent = 0;
                    using(sourceStream) {
                        while(true){
                            // cancel if cancelation token requested
                            if(cancellationToken.HasValue){
                                cancellationToken.Value.ThrowIfCancellationRequested();
                            }

                            var buffer = new Byte[_config.transferBufferSizeBytes];
                            var length = await sourceStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                            if(length <= 0) break;                                   

                            // check timeouts when writing
                            var uploadWriteTask = uploadStream.WriteAsync(buffer, 0, length);
                            if(await Task.WhenAny(uploadWriteTask, Task.Delay(_config.uploadRequestWriteTimeoutMilliseconds)).ConfigureAwait(false) == uploadWriteTask){
                                // all good                               
                            }else{
                                // timed-out
                                // close streams
                                sourceStream.Close();
                                uploadStream.Close();
                                // throw canceled exception
                                throw new TimeoutException("Upload write chunk timed-out");
                            }                            

                            totalBytesSent += length;

                            if(uploadProgress != null){
                                uploadProgress.Report((int)(totalBytesSent * 100 / payload.Length));
                            }                            
                        }

                        if(uploadProgress != null){
                            uploadProgress.Report(100);
                        }

                        // Debug.Log($"Total bytes sent: {totalBytesSent}");
                    }                    
                });

                streamContent.Headers.ContentType = contentType;                
                requestMessage.Content = streamContent;

                // disable upload buffering
                // requestMessage.Headers.TransferEncodingChunked = true;
                streamContent.Headers.ContentLength = payload.Length;                

                // GET RESPONSE HEADERS
                var getHeadersCTS = cancellationToken.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value) : new CancellationTokenSource();                
                var headersResponse = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, getHeadersCTS.Token);        

                try {
                    // throw exception if not success status code
                    headersResponse.EnsureSuccessStatusCode();       
                }catch(HttpRequestException ex){
                    await Utils.RethrowDropboxHttpRequestException(ex, headersResponse, _parameters, _endpoint);                    
                }                

                
                var contentLength = headersResponse.Content.Headers.ContentLength;                

                // READ RESPONSE BODY
                using(MemoryStream memStream = new MemoryStream()){
                    using (Stream responseStream = await headersResponse.Content.ReadAsStreamAsync ()) {                        

                        byte[] buffer = new byte[_config.transferBufferSizeBytes];
                        long totalBytesRead = 0;

                        while(true){
                            var readToBufferTask = responseStream.ReadAsync (buffer, 0, buffer.Length);
                            if(await Task.WhenAny(readToBufferTask, Task.Delay(_config.downloadChunkReadTimeoutMilliseconds)) == readToBufferTask){
                                int bytesRead = readToBufferTask.Result;

                                // exit loop condition
                                if(bytesRead <= 0){
                                    break;
                                }

                                if(cancellationToken.HasValue){
                                    cancellationToken.Value.ThrowIfCancellationRequested();
                                }                            

                                await memStream.WriteAsync (buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                if(downloadProgress != null && contentLength.HasValue){
                                    downloadProgress.Report((int)(totalBytesRead * 100 / contentLength.Value));
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

                    var responseBytes = memStream.ToArray();

                    // interpret bytes from memStream
                    var responseString = Encoding.UTF8.GetString(responseBytes);

                    if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
                        return default(RESP_T);
                    }

                    // Debug.Log($"Received request response: {responseString}");

                    responseString = Utils.FixDropboxJSONString(responseString);

                    var response = UnityEngine.JsonUtility.FromJson<RESP_T>(responseString);

                    return response;
                }
                
            }
        }        
    }

}