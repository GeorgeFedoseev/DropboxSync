
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


        public async Task<RESP_T> ExecuteAsync(byte[] payload = null, IProgress<int> progress = null, CancellationToken? cancellationToken = null, int timeoutMilliseconds = int.MaxValue){

            using (var client = new HttpClient()){
                
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpoint);

                var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);

                // TODO: add auth parameters if needed
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

                // TODO: add httpContent and set content type header
                var streamContent = new ProgressableStreamContent(new MemoryStream(payload), (sourceStream, uploadStream) => {
                    // TODO: write to uploadStream buffered
                    // check timeouts when writing
                    // cancel if cancelation token requested
                });

                streamContent.Headers.ContentType = contentType;                
                requestMessage.Content = streamContent;

                // disable upload buffering
                requestMessage.Headers.TransferEncodingChunked = true;
                streamContent.Headers.ContentLength = payload.Length;                

                var headersResponse = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                

                // TODO: get response stream, read to buffer with timeouts and measure download progress

            }

            // using (var client = new WebClientWithTimeout()){
                
            //     // check if timeout overriden
            //     if(timeoutMilliseconds != int.MaxValue){
            //         _timeoutMilliseconds = timeoutMilliseconds;
            //     }

            //     if(cancellationToken != null){
            //         cancellationToken.Value.Register(client.CancelAsync);
            //     }               

            //     var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);
                
            //     if(payload != null){
            //         // add parameters in header
            //         client.Headers.Set ("Dropbox-API-Arg", parametersJSONString);                     
            //     }

            //     if(_requiresAuthorization) {
            //         client.Headers.Set("Authorization", $"Bearer {_config.accessToken}");
            //     }
                
			// 	client.Headers.Set("Content-Type", payload == null ? "application/json" : "application/octet-stream");

            //     if(progress != null) {
            //         client.UploadProgressChanged += (object sender, UploadProgressChangedEventArgs e) => {
            //             // Debug.Log($"{e.BytesSent}/{e.TotalBytesToSend}");
            //             progress.Report((int)(e.BytesSent*100/e.TotalBytesToSend));
            //         };
            //     }                

            //     byte[] responseBytes = null; 
            //     try {

            //         Task<byte[]> uploadDataTask;                    
            //         if(payload == null){
            //             var paramatersBytes = Encoding.Default.GetBytes(parametersJSONString);
            //             uploadDataTask = client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", paramatersBytes);
            //         }else{
            //             // parameters are in header
            //             uploadDataTask = client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", payload);
            //         }
            //         // Debug.Log($"Request with timeout {_timeoutMilliseconds}ms");
            //         if (await Task.WhenAny(uploadDataTask, Task.Delay(_timeoutMilliseconds)) == uploadDataTask) {                
            //             // get response bytes
            //             responseBytes = uploadDataTask.Result;
            //         }else{
            //             // timeout
            //             // cancel upload task
            //             client.CancelAsync();
            //             throw new TimeoutException("Request timed out");
            //         }
            //     }catch (WebException ex){
            //         Utils.RethrowDropboxRequestWebException(ex, _parameters, _endpoint);                 
            //     }

            //     var responseString = Encoding.UTF8.GetString(responseBytes);

            //     if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
            //         return default(RESP_T);
            //     }

            //     // Debug.Log($"Received request response: {responseString}");

            //     responseString = Utils.FixDropboxJSONString(responseString);

            //     var response = UnityEngine.JsonUtility.FromJson<RESP_T>(responseString);

            //     return response;
            // }
        }
        
    }

}