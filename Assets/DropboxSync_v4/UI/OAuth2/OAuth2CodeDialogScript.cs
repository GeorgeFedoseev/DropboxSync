using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DBXSync {
    public class OAuth2CodeDialogScript : MonoBehaviour {

        public Action<string> onCodeSubmit = (_) => { };

        public Button connectButton, cancelButton;
        public InputField codeInput;
        public Text errorText;

        private void Start() {
            cancelButton.onClick.AddListener(() => {
                Close();
            });

            connectButton.onClick.AddListener(() => {
                connectButton.interactable = false; // prevent pressing several times
                onCodeSubmit(codeInput.text);
            });
        }
        public void DisplayError(string message) {
            errorText.text = message;
            connectButton.interactable = true;
        }

        public void Close() {
            Destroy(gameObject);
        }
    }
}


