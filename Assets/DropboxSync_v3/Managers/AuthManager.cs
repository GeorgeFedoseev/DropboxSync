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
            OpenCodeDialog((code) => {
                Debug.Log($"Got code: {code}");
            });
        }

        private void OpenCodeDialog(Action<string> onCodeSubmitted){
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
        }


        public void Dispose(){

        }
    }

}