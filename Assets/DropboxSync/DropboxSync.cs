using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using System.Threading.Tasks;

public class DropboxSync : MonoBehaviour {

    private static DropboxSync _instance;
		public static DropboxSync Main {
			get {
				if(_instance == null){
					_instance = FindObjectOfType<DropboxSync>();
					if(_instance != null){						
					}else{
						Debug.LogError("DropboxSync script wasn't found on the scene.");						
					}
				}
				return _instance;				
			}
		}

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

    public async Task<GetFileMetadataResponse> GetFileMetadataAsync(string dropboxFilePath){
        var request = new GetFileMetadataRequest(new GetMetadataRequestParameters {
            path = dropboxFilePath
        }, _configuration);

        return await request.ExecuteAsync();
    }


}
