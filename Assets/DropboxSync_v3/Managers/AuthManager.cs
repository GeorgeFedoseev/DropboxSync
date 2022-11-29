using System;
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
                return JsonUtility.FromJson<OAuth2TokenResponse>(PlayerPrefs.GetString("DBXAuth"));
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
            var url = $"https://www.dropbox.com/oauth2/authorize?client_id={_dropboxAppKey}&response_type=code";
            
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

                responseString = Utils.FixDropboxJSONString(responseString);
                return UnityEngine.JsonUtility.FromJson<OAuth2TokenResponse>(responseString);
            }
        }

        // TODO: method for exchanging refresh_token for new access_token

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