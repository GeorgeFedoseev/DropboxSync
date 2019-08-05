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
            LaunchOAuth2Flow("bsvn3tdn6jmsvg0");
        });
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LaunchOAuth2Flow(string client_id){
        var url = $"https://www.dropbox.com/oauth2/authorize?client_id={client_id}&response_type=code";
        Application.OpenURL(url);
    }
}
