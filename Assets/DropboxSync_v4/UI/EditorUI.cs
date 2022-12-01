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
    public class DropboxSyncEditorUI : Editor {

        private string _requestingCode = null;

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            var dropboxSync = (DropboxSync)target;
            if (dropboxSync == null) {
                return;
            }

            if (string.IsNullOrEmpty(dropboxSync._dropboxAppKey) || string.IsNullOrEmpty(dropboxSync._dropboxAppSecret)) {
                EditorGUILayout.LabelField("Please specify valid app key and app secret from Dropbox App Console.", EditorStyles.helpBox);
            } else {
                EditorGUILayout.LabelField("Specify refresh token above if you want to use single Dropbox account (provided by you) for all users.", EditorStyles.helpBox);

                if (dropboxSync != null) {
                    if (_requestingCode == null) {
                        if (GUILayout.Button("Get Refresh Token")) {
                            var url = $"https://www.dropbox.com/oauth2/authorize?client_id={dropboxSync._dropboxAppKey}&response_type=code&token_access_type=offline";
                            Application.OpenURL(url);
                            _requestingCode = "";
                        }
                    } else {
                        _requestingCode = EditorGUILayout.TextField("Enter code", _requestingCode);
                        if (GUILayout.Button("Submit")) {
                            var res = Task.Run(() => AuthManager.ExchangeCodeForAccessTokenAsync(_requestingCode, dropboxSync._dropboxAppKey, dropboxSync._dropboxAppSecret)).Result;
                            dropboxSync._dropboxRefreshToken = res.refresh_token;
                            _requestingCode = null;
                        }
                        if (GUILayout.Button("Cancel")) {
                            _requestingCode = null;
                        }
                    }
                }
            }

        }

    }

}

#endif