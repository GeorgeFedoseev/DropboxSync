using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.IO;

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
    // public CacheManager CacheManager {
    //     get {
    //         return _cacheManager;
    //     }
    // }

    private ChangesManager _changesManager;
    // public ChangesManager ChangesManager {
    //     get {
    //         return _changesManager;
    //     }
    // }

    private SyncManager _syncManager;
    // public SyncManager SyncManager {
    //     get {
    //         return _syncManager;
    //     }
    // }


    void Awake(){        
        // set configuration based on inspector values
        SetConfiguration(new DropboxSyncConfiguration { accessToken = _dropboxAccessToken});

        // DropboxReachability.Main.Initialize(_configuration.dropboxReachabilityCheckIntervalMilliseconds);

        _transferManger = new TransferManager(_config);
        _cacheManager = new CacheManager(_transferManger, _config);
        _changesManager = new ChangesManager(_cacheManager, _transferManger, _config);
        _syncManager = new SyncManager(_cacheManager, _changesManager, _config);
    }


    // METHODS

    public void SetConfiguration(DropboxSyncConfiguration config){
        _config = config;
        _config.FillDefaultsAndValidate();        
    }
    
    
    // public async Task<Metadata> GetMetadataAsync(string dropboxPath){
    //     var request = new GetMetadataRequest(new GetMetadataRequestParameters {
    //         path = dropboxPath
    //     }, _config);

    //     return (await request.ExecuteAsync()).GetMetadata();
    // }    

    // DOWNLOADING

    // as cached path
    public async Task<string> GetFileAsLocalCachedPathAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken){
        return await _cacheManager.GetLocalFilePathAsync(dropboxPath, progressCallback, cancellationToken);
    }

    public async void GetFileAsLocalCachedPath(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                                 Action<string> successCallback, Action<Exception> errorCallback,
                                                 bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                                 CancellationToken? cancellationToken = null)
    {
        try {
            Metadata lastServedMetadata = null;
            var serveCachedFirst = useCachedFirst || (Application.internetReachability == NetworkReachability.NotReachable && useCachedIfOffline);
            if(serveCachedFirst && _cacheManager.HaveFileLocally(dropboxPath)){
                lastServedMetadata = _cacheManager.GetLocalMetadataForDropboxPath(dropboxPath);
                successCallback(Utils.DropboxPathToLocalPath(dropboxPath, _config));
            }
            
            var resultPath = await GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken);
            var latestMetadata = _cacheManager.GetLocalMetadataForDropboxPath(dropboxPath);
            bool shouldServe = lastServedMetadata == null || lastServedMetadata.content_hash != latestMetadata.content_hash;
            // don't serve same version again
            if(shouldServe){
                successCallback(resultPath);
            }            

            if(receiveUpdates){                
                Action<EntryChange> syncedChangecallback = async (change) => {
                    // serve updated version
                    var updatedResultPath = await GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken);
                    successCallback(updatedResultPath);
                };

                KeepSynced(dropboxPath, syncedChangecallback);

                // unsubscribe from receiving updates when cancellation requested
                if(cancellationToken.HasValue){
                    cancellationToken.Value.Register(() => {
                        UnsubscribeFromKeepSyncCallback(dropboxPath, syncedChangecallback);
                    });
                }
            }            
            
        }catch(Exception ex){
            errorCallback(ex);
        }
    }

    // as bytes
    public async Task<byte[]> GetFileAsBytesAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken){
        var cachedFilePath = await GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken);
        return File.ReadAllBytes(cachedFilePath);
    }

    public void GetFileAsBytes(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                        Action<byte[]> successCallback, Action<Exception> errorCallback,
                                        bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                        CancellationToken? cancellationToken = null)
    {
        GetFileAsLocalCachedPath(dropboxPath, progressCallback, (localPath) => {
            successCallback(File.ReadAllBytes(localPath));
        }, errorCallback, useCachedFirst, useCachedIfOffline, receiveUpdates, cancellationToken);
    }

    // as T
    public async Task<T> GetFile<T>(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) where T : class{
        var bytes = await GetFileAsBytesAsync(dropboxPath, progressCallback, cancellationToken);
        return Utils.ConvertBytesTo<T>(bytes);
    }

    public void GetFile<T>(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                        Action<T> successCallback, Action<Exception> errorCallback,
                                        bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                        CancellationToken? cancellationToken = null) where T : class
    {
        GetFileAsBytes(dropboxPath, progressCallback, (bytes) => {
            successCallback(Utils.ConvertBytesTo<T>(bytes));
        }, errorCallback, useCachedFirst, useCachedIfOffline, receiveUpdates, cancellationToken);
    }

    // KEEP SYNCED
    public void KeepSynced(string dropboxPath, Action<EntryChange> syncedCallback){
        _syncManager.KeepSynced(dropboxPath, syncedCallback);
    }

    public void UnsubscribeFromKeepSyncCallback(string dropboxPath, Action<EntryChange> syncedCallback){
        _syncManager.UnsubscribeFromKeepSyncCallback(dropboxPath, syncedCallback);
    }

    public void StopKeepingInSync(string dropboxPath){
        _syncManager.StopKeepingInSync(dropboxPath);
    }

    public bool IsKeepingInSync(string dropboxPath){
        return _syncManager.IsKeepingInSync(dropboxPath);
    }


    

    // EVENTS

    void OnApplicationQuit(){
        print("OnApplicationQuit()");
        
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
