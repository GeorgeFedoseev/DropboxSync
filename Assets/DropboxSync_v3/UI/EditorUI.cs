# if UNITY_EDITOR

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DBXSync {

    [CustomEditor(typeof(DropboxSync))]
    public class DropboxSyncEditorUI: Editor {

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var dropboxSync = (DropboxSync) target;

            EditorGUILayout.LabelField("Specify refresh token above if you want to use single Dropbox account (provided by you) for all users.", EditorStyles.helpBox);

            if(dropboxSync != null && dropboxSync._dropboxRefreshToken.Trim().Length == 0){
            //     if(GUILayout.Button("Clear refresh token")) {
            //         dropboxSync._dropboxRefreshToken = "";
            //     }
            // } else {
                if(GUILayout.Button("Get Refresh Token")) {
                    // dropboxSync._dropboxRefreshToken = "TEST";
                    var url = $"https://www.dropbox.com/oauth2/authorize?client_id={dropboxSync._dropboxAppKey}&response_type=code&token_access_type=offline";
                    Application.OpenURL(url);
                    // EditorApplication.delayCall += () => {
                    EditorInputDialog.Show("Enter code from Dropbox OAuth2 flow", "Enter code here", (code) => {
                        var res = Task.Run(() => AuthManager.ExchangeCodeForAccessTokenAsync(code, dropboxSync._dropboxAppKey, dropboxSync._dropboxAppSecret)).Result;
                        dropboxSync._dropboxRefreshToken = res.refresh_token; 
                    });
                        
                    // };
                    // dropboxSync._dropboxRefreshToken = val;
                }
            }
                
        }

        // public static async Task<OAuth2TokenResponse> ExchangeCodeForAccessTokenAsync(string code, string appKey, string appSecret){
        //     return await GetPostResponse<OAuth2TokenResponse>(new Uri("https://api.dropbox.com/oauth2/token"), new List<KeyValuePair<string, string>>{
        //         new KeyValuePair<string, string>("grant_type", "authorization_code"),
        //         new KeyValuePair<string, string>("code", code),
        //     }, GetAuthenticationHeaderValue(appKey, appSecret));
        // }

        // public static async Task<T> GetPostResponse<T>(Uri uri, List<KeyValuePair<string, string>> data, 
        //     System.Net.Http.Headers.AuthenticationHeaderValue authenticationHeaderValue = null
        // ){
        //     var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri);
        //     requestMessage.Headers.Authorization = authenticationHeaderValue;
        //     requestMessage.Content = new FormUrlEncodedContent(data);

        //     using (var client = new HttpClient()) {
        //         var response = await client.SendAsync(requestMessage);
        //         try {
        //             // throw exception if not success status code
        //             response.EnsureSuccessStatusCode();       
        //         }catch(HttpRequestException ex){
        //             await Utils.RethrowDropboxHttpRequestException(ex, response, new RequestParameters(), uri.ToString());                    
        //         } 

        //         var responseString = await response.Content.ReadAsStringAsync();

        //         if(string.IsNullOrWhiteSpace(responseString) || responseString == "null"){
        //             Debug.LogError($"Failed to get access token: response is null");
        //             return default(T);
        //         }

        //         return Utils.GetDropboxResponseFromJSON<T>(responseString);
        //     }
        // }

    }
    
}

#endif