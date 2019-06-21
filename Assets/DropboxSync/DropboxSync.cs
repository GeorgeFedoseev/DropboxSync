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

    private DropboxSyncConfiguration _config;
    private TransferManager _transferManger;
    public TransferManager TransferManager {
        get {
            return _transferManger;
        }
    }

    private CacheManager _cacheManager;
    public CacheManager CacheManager {
        get {
            return _cacheManager;
        }
    }

    private ChangesManager _changesManager;
    public ChangesManager ChangesManager {
        get {
            return _changesManager;
        }
    }

    private SyncManager _syncManager;
    public SyncManager SyncManager {
        get {
            return _syncManager;
        }
    }


    void Awake(){        
        // set configuration based on inspector values
        SetConfiguration(new DropboxSyncConfiguration { accessToken = _dropboxAccessToken});

        // DropboxReachability.Main.Initialize(_configuration.dropboxReachabilityCheckIntervalMilliseconds);

        _transferManger = new TransferManager(_config);
        _cacheManager = new CacheManager(_transferManger, _config);
        _changesManager = new ChangesManager(_cacheManager, _config);
        _syncManager = new SyncManager(_cacheManager, _changesManager, _config);
    }


    // METHODS

    public void SetConfiguration(DropboxSyncConfiguration config){
        _config = config;
        _config.FillDefaultsAndValidate();        

        // DropboxReachability.Main.SetPingInterval(_configuration.dropboxReachabilityCheckIntervalMilliseconds);
    }
    

    public async Task<Metadata> GetFileMetadataAsync(string dropboxFilePath){
        var request = new GetMetadataRequest(new GetMetadataRequestParameters {
            path = dropboxFilePath
        }, _config);

        return (await request.ExecuteAsync()).GetMetadata();
    }

    

    // EVENTS

    void OnApplicationQuit(){
        print("OnApplicationQuit()");
        // DropboxReachability.Main.Dispose();
        if(_transferManger != null){
            _transferManger.Dispose();
        }
        if(_changesManager != null){
            _changesManager.Dispose();
        }
        if(_syncManager != null){
            _syncManager.Dispose();
        }
    }


}
