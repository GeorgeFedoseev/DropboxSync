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

        private Action<OAuth2TokenResponse> _onFlowCompletedAction = (_) => { };

        private OAuth2CodeDialogScript _currentDialog;

        private Task<String> _refreshTokenTask;


        public AuthManager(string dropboxAppKey, string dropboxAppSecret, Action<OAuth2TokenResponse> onFlowCompleted) {
            // verify app key is valid
            if (string.IsNullOrWhiteSpace(dropboxAppKey)) {
                Debug.LogWarning("[DropboxSync] Dropbox app key is not set or invalid");
                // throw new InvalidConfigurationException("Dropbox app key is not set or invalid");
            }
            _dropboxAppKey = dropboxAppKey;
            _dropboxAppSecret = dropboxAppSecret;
            _onFlowCompletedAction = onFlowCompleted;
        }


        // PRESERVING ACCESS TOKEN
        private void SaveAuthentication(OAuth2TokenResponse tokenResult) {
            PlayerPrefs.SetString("DBXAuth_v4", JsonUtility.ToJson(tokenResult));
            PlayerPrefs.Save();
        }

        public OAuth2TokenResponse GetSavedAuthentication() {
            if (PlayerPrefs.HasKey("DBXAuth_v4")) {
                return Utils.GetDropboxResponseFromJSON<OAuth2TokenResponse>(PlayerPrefs.GetString("DBXAuth_v4"));
            }
            return null;
        }

        public void DropSavedAthentication() {
            PlayerPrefs.DeleteKey("DBXAuth_v4");
            PlayerPrefs.Save();
        }


        // OAUTH2 FLOW

        public void LaunchOAuth2Flow() {
            // open OAuth2 flow in browser

            // TODO: add token_access_type=offline query param (so that code flow returns both short-lived access_token and long-lived refresh_token)
            var url = $"https://www.dropbox.com/oauth2/authorize?client_id={_dropboxAppKey}&response_type=code&token_access_type=offline";

            Application.OpenURL(url);

            // TODO: open dialog with code input in Unity
            _currentDialog = OpenCodeDialog(OnCodeSubmitted);
        }

        private OAuth2CodeDialogScript OpenCodeDialog(Action<string> onCodeSubmitted) {
            // remove existing dialogs if exist
            foreach (var d in Resources.FindObjectsOfTypeAll<OAuth2CodeDialogScript>()) {
                if (d.gameObject.scene.name != null) { // if object is in scene and not prefab
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


        public static async Task<OAuth2TokenResponse> ExchangeCodeForAccessTokenAsync(string code, string appKey, string appSecret) {
            return await Utils.GetPostResponse<OAuth2TokenResponse>(new Uri("https://api.dropbox.com/oauth2/token"), new List<KeyValuePair<string, string>>{
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
            }, GetAuthenticationHeaderValue(appKey, appSecret));
        }


        public async Task<string> RefreshAccessToken() {
            if (_refreshTokenTask == null) {
                _refreshTokenTask = _RefreshAccessTokenAsync();
            } else {
                Debug.LogWarning($"[DropboxSync] Already refreshing access_token, waiting for finish...");
            }

            try {
                return await _refreshTokenTask;
            } finally {
                _refreshTokenTask = null;
            }
        }

        private async Task<string> _RefreshAccessTokenAsync() {
            // check if have refresh_token
            var savedAuth = GetSavedAuthentication();
            if (savedAuth != null && string.IsNullOrEmpty(savedAuth.refresh_token)) {
                // remove saved auth because it doesn't have refresh_token
                DropSavedAthentication();
                throw new NoRefreshTokenException("No refresh_token saved to get new access_token");
            }

            try {
                var oauth_resp = await GetAuthWithRefreshTokenAsync(savedAuth.refresh_token);
                SaveAuthentication(oauth_resp);
                Debug.Log($"[DropboxSync] Access token was refreshed to {oauth_resp.access_token}");
                return oauth_resp.access_token;
            } catch (InvalidGrantTokenException) {
                DropSavedAthentication();
                throw;
            }
        }

        public async Task<OAuth2TokenResponse> GetAuthWithRefreshTokenAsync(string refreshToken) {
            var resp = await Utils.GetPostResponse<OAuth2TokenResponse>(
                new Uri("https://api.dropbox.com/oauth2/token"),
                new List<KeyValuePair<string, string>>{
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
            }, GetAuthenticationHeaderValue(_dropboxAppKey, _dropboxAppSecret));

            // manually set refresh token, as it's not provided in this response
            resp.refresh_token = refreshToken;

            return resp;

        }

        public string AuthWithRefreshTokenSync(string refreshToken) {
            try {
                var oauth_resp = Task.Run(() => GetAuthWithRefreshTokenAsync(refreshToken)).Result;
                oauth_resp.refresh_token = refreshToken;

                SaveAuthentication(oauth_resp);
                return oauth_resp.access_token;
            } catch (InvalidGrantTokenException) {
                DropSavedAthentication();
                throw;
            }

        }

        private static System.Net.Http.Headers.AuthenticationHeaderValue GetAuthenticationHeaderValue(string appKey, string appSecret) {
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(appKey + ":" + appSecret));
            return new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }


        private async void OnCodeSubmitted(string code) {
            var tokenResult = await ExchangeCodeForAccessTokenAsync(code, _dropboxAppKey, _dropboxAppSecret);
            if (tokenResult != null) {
                SaveAuthentication(tokenResult);
                _onFlowCompletedAction(tokenResult);
                _currentDialog.Close();
                _currentDialog = null;

            } else {
                _currentDialog.DisplayError("Wrong code");
            }
        }


    }

}