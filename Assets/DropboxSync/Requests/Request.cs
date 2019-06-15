
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.IO;

namespace DBXSync {

    public class Request<RESP_T> {

        private string _endpoint;
        private RequestParameters _parameters;
        private DropboxSyncConfiguration _config;
        private bool _parametersInBody;

        public Request(string endpoint, RequestParameters parameters, bool parametersInBody, DropboxSyncConfiguration config) {
            _endpoint = endpoint;
            _parameters = parameters;
            _config = config;
            _parametersInBody = parametersInBody;
        }


        public async Task<RESP_T> ExecuteAsync(IProgress<int> progress = null){
            using (var client = new WebClientWithTimeout()){
                var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);
                if(!_parametersInBody){
                    // add parameters in header
                    client.Headers.Set ("Dropbox-API-Arg", parametersJSONString); 
                }

                client.Headers.Set("Authorization", $"Bearer {_config.accessToken}");
				client.Headers.Set("Content-Type", _parametersInBody ? "application/json" : "application/octet-stream");

                if(progress != null) {
                    client.UploadProgressChanged += (object sender, UploadProgressChangedEventArgs e) => {
                        progress.Report(e.ProgressPercentage);
                    };
                }                

                byte[] responseBytes = null; 
                try {
                    if(_parametersInBody){
                        var paramatersBytes = Encoding.Default.GetBytes(parametersJSONString);
                        responseBytes = await client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", paramatersBytes);
                    }else{
                        // parameters are in header
                        responseBytes = await client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", new byte[0]);
                    }                    
                }catch (WebException ex){
                    Utils.HandleDropboxRequestWebException(ex, _parameters, _endpoint);                 
                }               

                var responseString = Encoding.UTF8.GetString(responseBytes);
                responseString = Utils.FixDropboxJSONString(responseString);

                var response = UnityEngine.JsonUtility.FromJson<RESP_T>(responseString);

                return response;
            }
        }
        
    }

}