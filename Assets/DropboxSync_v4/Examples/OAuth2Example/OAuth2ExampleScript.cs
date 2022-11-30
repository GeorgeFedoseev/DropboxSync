using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OAuth2ExampleScript : MonoBehaviour {

    public Button connectButton, disconnectButton;
    public Text authenticatedText;

    // Start is called before the first frame update
    void Start()
    {
        connectButton.onClick.AddListener(() => {
            DropboxSync.Main.AuthenticateWithOAuth2Flow();
        });

        disconnectButton.onClick.AddListener(() => {
            DropboxSync.Main.LogOut();
        });
        
    }

    // Update is called once per frame
    void Update() {
        connectButton.gameObject.SetActive(!DropboxSync.Main.IsAuthenticated);
        authenticatedText.gameObject.SetActive(DropboxSync.Main.IsAuthenticated);
        disconnectButton.gameObject.SetActive(DropboxSync.Main.IsAuthenticated);
    }

    
}
