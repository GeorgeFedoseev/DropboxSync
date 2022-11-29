using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class AuthManager {

        private string _dropboxAppKey;
        private string _dropboxAppSecret;

        private Action<OAuth2TokenResponse> _onFlowCompletedAction = (_) => {};

        private OAuth2CodeDialogScript _currentDialog;

        public AuthManager(string dropboxAppKey, string dropboxAppSecret, Action<OAuth2TokenResponse> onFlowCompleted){
            // verify app key is valid
            if(string.IsNullOrWhiteSpace(dropboxAppKey)){
                 Debug.LogWarning("[DropboxSync] Dropbox app key is not set or invalid");
                // throw new InvalidConfigurationException("Dropbox app key is not set or invalid");
            }
            _dropboxAppKey = dropboxAppKey;
            _dropboxAppSecret = dropboxAppSecret;
            _onFlowCompletedAction = onFlowCompleted;
        }


        // PRESERVING ACCESS TOKEN
        private void SaveAuthentication(OAuth2TokenResponse tokenResult){
            PlayerPrefs.SetString("DBXAuth", JsonUtility.ToJson(tokenResult));
            PlayerPrefs.Save();
        }

        public OAuth2TokenResponse GetSavedAuthentication(){
            if(PlayerPrefs.HasKey("DBXAuth")){                
                return Utils.GetDropboxResponseFromJSON<OAuth2TokenResponse>(PlayerPrefs.GetString("DBXAuth"));
            }
            return null;
        }

        public void DropSavedAthentication(){
            PlayerPrefs.DeleteKey("DBXAuth");
            PlayerPrefs.Save();
        }


        // OAUTH2 FLOW

        public void LaunchOAuth2Flow(){
            // open OAuth2 flow in browser

            // TODO: add token_access_type=offline query param (so that code flow returns both short-lived access_token and long-lived refresh_token)
            var url = $"https://www.dropbox.com/oauth2/authorize?client_id={_dropboxAppKey}&response_type=code&token_access_type=offline";
            
            Application.OpenURL(url);

            // TODO: open dialog with code input in Unity
            _currentDialog = OpenCodeDialog(OnCodeSubmitted);
        }

        private OAuth2CodeDialogScript OpenCodeDialog(Action<string> onCodeSubmitted){
            // remove existing dialogs if exist
            foreach(var d in Resources.FindObjectsOfTypeAll<OAuth2CodeDialogScript>()){                
                if(d.gameObject.scene.name != null){ // if object is in scene and not prefab
                    GameObject.Destroy(d.gameObject);
                }
            }

            // instantiate new dialog
            var dialog = (GameObject.Instantiate(Resources.Load("DropboxSyncOAuth2CodeDialog/OAuth2CodeDialog")) as GameObject)
                        .GetComponent<OAuth2CodeDialogScript>();
            dialog.onCodeSubmit = onCodeSubmitted;
            dialog.name = "DropboxSync_OAuth2CodeDialog";

            return dialog;
        }


        private async Task<OAuth2TokenResponse> ExchangeCodeForAccessTokenAsync(string code){
            var uri = new Uri("https://api.dropbox.com/oauth2/token");

            using(var client = new WebClient()){                               
                string credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes(_dropboxAppKey + ":" + _dropboxAppSecret));
                client.Headers[HttpRequestHeader.Authorization] = string.Format(
                "Basic {0}", credentials);

                NameValueCollection postValues = new NameValueCollection();
                postValues.Add("code", code);
                postValues.Add("grant_type", "authorization_code");

                byte[] responseBytes = null;
                try {
                    responseBytes = await client.UploadValuesTaskAsync(uri, "POST", postValues);
                }catch(WebException ex){
                    try {
                        using(var sr = new StreamReader(ex.Response.GetResponseStream())){
                            Debug.LogError($"Failed to get access token: {sr.ReadToEnd()}");
                        }
                    }catch{
                        Debug.LogError($"Failed to get access token: {ex}");
                    }                   
                    
                    return null;
                }
                
                var responseString = Encoding.UTF8.GetString(responseBytes);

                if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
                    Debug.LogError($"Failed to get access token: response is null");
                    return null;
                }

                return Utils.GetDropboxResponseFromJSON<OAuth2TokenResponse>(responseString);
            }
        }

        private Task<String> _refreshTokenTask;

        public async Task<string> RefreshAccessToken() {
            if(_refreshTokenTask == null){
                _refreshTokenTask = _RefreshAccessToken();
            }else{
                Debug.LogWarning($"Already refreshing access_token, waiting for finish...");
            }

            try {
                return await _refreshTokenTask;
            } finally {
                _refreshTokenTask = null;
            }           
        }

        private async Task<string> _RefreshAccessToken() {
            // get refresh token
            var savedAuth = GetSavedAuthentication();
            if(savedAuth != null && string.IsNullOrEmpty(savedAuth.refresh_token)){
                // remove saved auth because it doesn't have refresh_token
                DropSavedAthentication();
                throw new NoRefreshTokenException("No refresh_token saved to get new access_token");
            }

            
            var uri = new Uri("https://api.dropbox.com/oauth2/token");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri);
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(_dropboxAppKey + ":" + _dropboxAppSecret));
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            requestMessage.Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>{
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", savedAuth.refresh_token),
            });

            using (var client = new HttpClient()) {
                var response = await client.SendAsync(requestMessage);
                try {
                    // throw exception if not success status code
                    response.EnsureSuccessStatusCode();       
                }catch(HttpRequestException ex){
                    await Utils.RethrowDropboxHttpRequestException(ex, response, new RequestParameters(), uri.ToString());                    
                } 

                var responseString = await response.Content.ReadAsStringAsync();
                
                if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
                    Debug.LogError($"Failed to get access token: response is null");
                    return null;
                }

                var oauth_resp = Utils.GetDropboxResponseFromJSON<OAuth2TokenResponse>(responseString);

                // modify access_token in original auth data 
                // (refresh token response does not contain refresh_token, so we keep it from original auth data)
                savedAuth.access_token = oauth_resp.access_token;
                SaveAuthentication(savedAuth);

                return savedAuth.access_token;

            }
         

            // using(var client = new WebClient()){                               
            //     string credentials = Convert.ToBase64String(
            //     Encoding.ASCII.GetBytes(_dropboxAppKey + ":" + _dropboxAppSecret));
            //     client.Headers[HttpRequestHeader.Authorization] = string.Format(
            //     "Basic {0}", credentials);

            //     NameValueCollection postValues = new NameValueCollection();
            //     postValues.Add("refresh_token", savedAuth.refresh_token);
            //     postValues.Add("grant_type", "refresh_token");

            //     byte[] responseBytes = null;
            //     try {
            //         responseBytes = await client.UploadValuesTaskAsync(uri, "POST", postValues);
            //     }catch(WebException ex){
            //         try {
            //             using(var sr = new StreamReader(ex.Response.GetResponseStream())){
            //                 Debug.LogError($"Failed to get access token (output): {sr.ReadToEnd()}");
            //             }
            //         }catch{
            //             Debug.LogError($"Failed to get access token: {ex}");
            //         }                   
                    
            //         return null;
            //     }
                
            //     var responseString = Encoding.UTF8.GetString(responseBytes);

            //     if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
            //         Debug.LogError($"Failed to get access token: response is null");
            //         return null;
            //     }

            //     var oauth_resp = Utils.GetDropboxResponseFromJSON<OAuth2TokenResponse>(responseString);

            //     // modify access_token in original auth data 
            //     // (refresh token response does not contain refresh_token, so we keep it from original auth data)
            //     savedAuth.access_token = oauth_resp.access_token;
            //     SaveAuthentication(savedAuth);

            //     return savedAuth.access_token;
            // }
        }

        private async void OnCodeSubmitted(string code){            
            var tokenResult = await ExchangeCodeForAccessTokenAsync(code);
            if(tokenResult != null){
                SaveAuthentication(tokenResult);
                // Debug.Log($"Got access token: {tokenResult.access_token}");
                _onFlowCompletedAction(tokenResult);
                _currentDialog.Close();
                _currentDialog = null;
                
            }else{
                _currentDialog.DisplayError("Wrong code");
            }
        }


    }

}