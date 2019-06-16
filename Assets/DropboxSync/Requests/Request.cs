
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Threading;

namespace DBXSync {

    public class Request<RESP_T> {

        private string _endpoint;
        private RequestParameters _parameters;
        private DropboxSyncConfiguration _config;
        // private bool _parametersInBody;


        public Request(string endpoint, RequestParameters parameters, DropboxSyncConfiguration config) {
            _endpoint = endpoint;
            _parameters = parameters;
            _config = config;
        }

       


        public async Task<RESP_T> ExecuteAsync(byte[] payload = null, IProgress<int> progress = null, CancellationToken? cancellationToken = null){
            using (var client = new WebClientWithTimeout()){

                if(cancellationToken != null){
                    cancellationToken.Value.Register(client.CancelAsync);
                }
                

                var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);
                
                if(payload != null){
                    // add parameters in header
                    client.Headers.Set ("Dropbox-API-Arg", parametersJSONString);                     
                }

                client.Headers.Set("Authorization", $"Bearer {_config.accessToken}");
				client.Headers.Set("Content-Type", payload == null ? "application/json" : "application/octet-stream");

                

                if(progress != null) {
                    client.UploadProgressChanged += (object sender, UploadProgressChangedEventArgs e) => {
                        // Debug.Log($"{e.BytesSent}/{e.TotalBytesToSend}");
                        progress.Report((int)(e.BytesSent*100/e.TotalBytesToSend));
                    };
                }                

                byte[] responseBytes = null; 
                try {
                    if(payload == null){
                        var paramatersBytes = Encoding.Default.GetBytes(parametersJSONString);
                        responseBytes = await client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", paramatersBytes);
                    }else{
                        // parameters are in header
                        responseBytes = await client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", payload != null ? payload : new byte[0]);
                    }                    
                }catch (WebException ex){
                    Utils.HandleDropboxRequestWebException(ex, _parameters, _endpoint);                 
                }            


                

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