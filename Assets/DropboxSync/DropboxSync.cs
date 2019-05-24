using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;

public class DropboxSync : MonoBehaviour {

    // inspector
    [SerializeField]
    private string _dropboxAccessToken;

    private DropboxSyncConfiguration _configuration;


    void Awake(){
        // set configuration based on inspector values
        SetConfiguration(new DropboxSyncConfiguration { accessToken = _dropboxAccessToken});
    }


    // METHODS

    public void SetConfiguration(DropboxSyncConfiguration config){
        _configuration = config;
        _configuration.FillDefaultsAndValidate();
    }


}
