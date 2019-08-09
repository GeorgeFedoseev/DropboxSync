using System;
using UnityEngine;

namespace DBXSync {

    public class AuthManager : IDisposable {

        private string _dropboxAppKey;

        private Action<string> _onFlowCompletedAction = (_) => {};

        public AuthManager(string dropboxAppKey, Action<string> onFlowCompleted){
            // verify app key is valid
            if(string.IsNullOrWhiteSpace(dropboxAppKey)){
                throw new InvalidConfigurationException("Dropbox app key is not set or invalid");
            }
            _dropboxAppKey = dropboxAppKey;
            _onFlowCompletedAction = onFlowCompleted;
        }

        public void LaunchOAuth2Flow(){
            // open OAuth2 flow in browser
            var url = $"https://www.dropbox.com/oauth2/authorize?client_id={_dropboxAppKey}&response_type=code";
            Application.OpenURL(url);

            // TODO: open dialog with code input in Unity
        }


        public void Dispose(){

        }
    }

}