using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using System.Threading.Tasks;
using System;

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

        // DropboxReachability.Main.Initialize(_configuration.dropboxReachabilityCheckIntervalMilliseconds);
    }


    // METHODS

    public void SetConfiguration(DropboxSyncConfiguration config){
        _configuration = config;
        _configuration.FillDefaultsAndValidate();        

        // DropboxReachability.Main.SetPingInterval(_configuration.dropboxReachabilityCheckIntervalMilliseconds);
    }
    

    public async Task<FileMetadata> GetFileMetadataAsync(string dropboxFilePath){
        var request = new GetFileMetadataRequest(new GetMetadataRequestParameters {
            path = dropboxFilePath
        }, _configuration);

        return (await request.ExecuteAsync()).GetMetadata();
    }

    public async Task<FileMetadata> DownloadFileAsync(string dropboxPath, string localPath, IProgress<int> progress){
        var downloadTask = new DownloadFileTransfer(dropboxPath, localPath, _configuration).ExecuteAsync(progress);
        return await downloadTask;
    }

    // EVENTS

    void OnApplicationQuit(){
        print("OnApplicationQuit()");
        // DropboxReachability.Main.Dispose();
    }


}
