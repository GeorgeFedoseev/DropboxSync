using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OAuth2ExampleScript : MonoBehaviour {

    public Button connectButton;

    // Start is called before the first frame update
    void Start()
    {
        connectButton.onClick.AddListener(() => {
            DropboxSync.Main.AuthenticateWithOAuth2Flow();
        });
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
}
