
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DBXSync {

    public class Request<RESP_T> {

        private string _endpoint;
        private RequestParameters _parameters;
        private DropboxSyncConfiguration _config;

        public Request(string endpoint, RequestParameters parameters, DropboxSyncConfiguration config) {
            _endpoint = endpoint;
            _parameters = parameters;
            _config = config;
        }


        public async Task<RESP_T> ExecuteAsync(IProgress<int> progress = null){
            using (var client = new DBXWebClient()){
                client.Headers.Set("Authorization", $"Bearer {_config.accessToken}");
				client.Headers.Set("Content-Type", "application/json");

                if(progress != null) {
                    client.UploadProgressChanged += (object sender, UploadProgressChangedEventArgs e) => {
                        progress.Report(e.ProgressPercentage);
                    };
                }                

                var parametersJSONString = UnityEngine.JsonUtility.ToJson(_parameters);
                var paramatersBytes = Encoding.Default.GetBytes(parametersJSONString);

                var responseBytes = await client.UploadDataTaskAsync(new System.Uri(_endpoint), "POST", paramatersBytes);                ;

                var responseString = Encoding.UTF8.GetString(responseBytes);
                responseString = Utils.FixDropboxJSONString(responseString);

                var response = UnityEngine.JsonUtility.FromJson<RESP_T>(responseString);

                // TODO: throw Dropbox related exceptions based on JSON error_summary field

                return response;
            }
        }
        
    }

}