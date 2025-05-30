﻿using System.Collections;
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
            if (_instance == null) {
                _instance = FindObjectOfType<DropboxSync>();
                if (_instance == null) {
                    Debug.LogError("[DropboxSync] DropboxSync script wasn't found on the scene.");
                }
            }
            return _instance;
        }
    }

    // inspector

    // TODO: show error if these fields are empty
    [Header("Dropbox Authorization")]
    [SerializeField]
    public string _dropboxAppKey;
    [SerializeField]
    public string _dropboxAppSecret;

    [Header("Dropbox Authorization (Optional)")]

    [SerializeField]
    public string _dropboxRefreshToken;

    private DropboxSyncConfiguration _config;
    public DropboxSyncConfiguration Config {
        get {
            return _config;
        }
    }

    private TransferManager _transferManger;
    public TransferManager TransferManager {
        get {
            return _transferManger;
        }
    }

    private CacheManager _cacheManager;
    private ChangesManager _changesManager;
    private SyncManager _syncManager;
    private AuthManager _authManager;


    void Awake() {
        ValidateParameters();

        _config = new DropboxSyncConfiguration { appKey = _dropboxAppKey, appSecret = _dropboxAppSecret };
        _config.FillDefaultsAndValidate();

        _authManager = new AuthManager(_dropboxAppKey, _dropboxAppSecret, OnOAuth2FlowCompleted);

        var savedAuth = _authManager.GetSavedAuthentication();

        // check if specified refresh token
        if (!string.IsNullOrEmpty(_dropboxRefreshToken)) {
            if(savedAuth != null && _dropboxRefreshToken != savedAuth.refresh_token) {
                // specified refresh token has more priority, so drop saved in PlayerPrefs tokens
                Debug.LogWarning($"[DropboxSync] Dropping saved credentials due to specified refresh token doesn't match one in saved creadentials.");
                _authManager.DropSavedAthentication();
                savedAuth = null;
            }
        }

        if (savedAuth != null) {
            Debug.Log("[DropboxSync] Found cached access_token");
            // if have access_token saved, use that
            InitializeWithAccessToken(savedAuth.access_token);
        } // else will use specified refresh token (if given) or user should authenticate using OAuth2 flow in UI
    }

    // INIT

    private void ValidateParameters() {
        if (string.IsNullOrEmpty(_dropboxAppKey)) {
            Debug.LogError("[DropboxSync] Please specify a valid Dropbox App Key (from Dropbox app console).");
        }

        if (string.IsNullOrEmpty(_dropboxAppSecret)) {
            Debug.LogError("[DropboxSync] Please specify a valid Dropbox App Secret (from Dropbox app console).");
        }
    }





    private void InitializeWithAccessToken(string accessToken) {
        _config.SetAccessToken(accessToken);

        DisposeManagers();
        _transferManger = new TransferManager(_config);
        _cacheManager = new CacheManager(_transferManger, _config);
        _changesManager = new ChangesManager(_cacheManager, _transferManger, _config);
        _syncManager = new SyncManager(_cacheManager, _changesManager, _config);

        Debug.Log("[DropboxSync] Initialized");
    }

    // AUTHENTICATION
    public void AuthenticateWithOAuth2Flow() {
        _authManager.LaunchOAuth2Flow();
    }

    private void OnOAuth2FlowCompleted(OAuth2TokenResponse tokenResult) {
        Debug.Log("[DropboxSync] Authorized using OAuth2 Flow");
        InitializeWithAccessToken(tokenResult.access_token);
    }

    public async Task RefreshAccessToken() {
        Debug.Log("[DropboxSync] Refreshing access_token...");
        try {
            var accessToken = await _authManager.RefreshAccessToken();
            _config.SetAccessToken(accessToken);
        } catch (Exception ex) {
            Debug.LogError($"[DropboxSync] Failed to refresh access_token: {ex}");
            _config.InvalidateAccessToken();
            DisposeManagers();
        }
    }

    public void LogOut() {
        _authManager.DropSavedAthentication();
        DisposeManagers();
        _config.InvalidateAccessToken();
        Debug.Log("[DropboxSync] Logged out");
    }

    public bool IsAuthenticated => _config != null && _config.accessToken != null;


    private void ThrowIfNotAuthenticated() {

        if (!IsAuthenticated) {
            // if not authenticated and have refreshToken param - authenticate
            if (!string.IsNullOrEmpty(_dropboxRefreshToken) && _authManager != null) {
                var access_token = _authManager.AuthWithRefreshTokenSync(_dropboxRefreshToken);
                Debug.Log("[DropboxSync] Authorized using predefined refresh token");
                InitializeWithAccessToken(access_token);
            } else {
                throw new DropboxNotAuthenticatedException("Tried to call Dropbox API without authentication. Please authenticate using OAuth2 flow UI or specify refresh token parameter in DropboxSync component.");
            }
        }
    }



    // DOWNLOADING

    // as cached path

    /// <summary>
    /// Asynchronously retrieves file from Dropbox and returns path to local filesystem cached copy
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns>Task that produces path to downloaded file</returns>
    public async Task<string> GetFileAsLocalCachedPathAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken = null) {
        ThrowIfNotAuthenticated();
        return await _cacheManager.GetLocalFilePathAsync(dropboxPath, progressCallback, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves file from Dropbox and returns path to local filesystem cached copy
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="successCallback">Callback for receiving downloaded file path</param>
    /// <param name="errorCallback">Callback that is triggered if any exception happened</param>
    /// <param name="useCachedFirst">Serve cached version (if it exists) before event checking Dropbox for newer version?</param>
    /// <param name="useCachedIfOffline">Use cached version if no Internet connection?</param>
    /// <param name="receiveUpdates">If `true`, then when there are remote updates on Dropbox, callback function `successCallback ` will be triggered again with updated version of the file.</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns></returns>
    public void GetFileAsLocalCachedPath(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                                 Action<string> successCallback, Action<Exception> errorCallback,
                                                 bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                                 CancellationToken? cancellationToken = null) {
        ThrowIfNotAuthenticated();
        try {
            if (receiveUpdates) {
                Action<EntryChange> syncedChangecallback = async (change) => {
                    // serve updated version
                    var updatedResultPath = await GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken);
                    successCallback(updatedResultPath);
                };

                KeepSynced(dropboxPath, syncedChangecallback);

                // unsubscribe from receiving updates when cancellation requested
                if (cancellationToken.HasValue) {
                    cancellationToken.Value.Register(() => {
                        UnsubscribeFromKeepSyncCallback(dropboxPath, syncedChangecallback);
                    });
                }
            }

            Metadata lastServedMetadata = null;
            var serveCachedFirst = useCachedFirst || (Application.internetReachability == NetworkReachability.NotReachable && useCachedIfOffline);
            if (serveCachedFirst && _cacheManager.HaveFileLocally(dropboxPath)) {
                lastServedMetadata = _cacheManager.GetLocalMetadataForDropboxPath(dropboxPath);
                successCallback(Utils.DropboxPathToLocalPath(dropboxPath, _config));
            }

            GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken).ContinueWith((t) => {
                if (t.Exception != null) {
                    errorCallback(t.Exception);
                } else {
                    var latestMetadata = _cacheManager.GetLocalMetadataForDropboxPath(dropboxPath);
                    bool shouldServe = lastServedMetadata == null || lastServedMetadata.content_hash != latestMetadata.content_hash;
                    // don't serve same version again
                    if (shouldServe) {
                        successCallback(t.Result);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);

        } catch (Exception ex) {
            errorCallback(ex);
        }
    }

    // as bytes

    /// <summary>
    /// Asynchronously retrieves file from Dropbox and returns it as byte array
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns>Task that produces byte array</returns>
    public async Task<byte[]> GetFileAsBytesAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) {
        ThrowIfNotAuthenticated();
        var cachedFilePath = await GetFileAsLocalCachedPathAsync(dropboxPath, progressCallback, cancellationToken);
        return File.ReadAllBytes(cachedFilePath);
    }

    /// <summary>
    /// Asynchronously retrieves file from Dropbox and returns it as byte array
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="successCallback">Callback for receiving downloaded file bytes</param>
    /// <param name="errorCallback">Callback that is triggered if any exception happened</param>
    /// <param name="useCachedFirst">Serve cached version (if it exists) before event checking Dropbox for newer version?</param>
    /// <param name="useCachedIfOffline">Use cached version if no Internet connection?</param>
    /// <param name="receiveUpdates">If `true`, then when there are remote updates on Dropbox, callback function `successCallback ` will be triggered again with updated version of the file.</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns></returns>
    public void GetFileAsBytes(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                        Action<byte[]> successCallback, Action<Exception> errorCallback,
                                        bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                        CancellationToken? cancellationToken = null) {
        ThrowIfNotAuthenticated();
        GetFileAsLocalCachedPath(dropboxPath, progressCallback, (localPath) => {
            successCallback(File.ReadAllBytes(localPath));
        }, errorCallback, useCachedFirst, useCachedIfOffline, receiveUpdates, cancellationToken);
    }

    // as T

    /// <summary>
    /// Retrieves file from Dropbox and returns it as T (T can be string, Texture2D or any type that can be deserialized from text using JsonUtility)
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns>Task that produces object of type T</returns>
    public async Task<T> GetFileAsync<T>(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) where T : class {
        ThrowIfNotAuthenticated();
        var bytes = await GetFileAsBytesAsync(dropboxPath, progressCallback, cancellationToken);
        return Utils.ConvertBytesTo<T>(bytes);
    }

    /// <summary>
    /// Retrieves file from Dropbox and returns it as T (T can be string, Texture2D or any type that can be deserialized from text using JsonUtility)
    /// </summary>
    /// <param name="dropboxPath">Path to file on Dropbox</param>
    /// <param name="progressCallback">Progress callback with download percentage and speed</param>
    /// <param name="successCallback">Callback for receiving downloaded object T (T can be string, Texture2D or any type that can be deserialized from text using JsonUtility)</param>
    /// <param name="errorCallback">Callback that is triggered if any exception happened</param>
    /// <param name="useCachedFirst">Serve cached version (if it exists) before event checking Dropbox for newer version?</param>
    /// <param name="useCachedIfOffline">Use cached version if no Internet connection?</param>
    /// <param name="receiveUpdates">If `true`, then when there are remote updates on Dropbox, callback function `successCallback ` will be triggered again with updated version of the file.</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel download</param>
    /// <returns></returns>
    public void GetFile<T>(string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                        Action<T> successCallback, Action<Exception> errorCallback,
                                        bool useCachedFirst = false, bool useCachedIfOffline = true, bool receiveUpdates = false,
                                        CancellationToken? cancellationToken = null) where T : class {
        ThrowIfNotAuthenticated();
        GetFileAsBytes(dropboxPath, progressCallback, (bytes) => {
            successCallback(Utils.ConvertBytesTo<T>(bytes));
        }, errorCallback, useCachedFirst, useCachedIfOffline, receiveUpdates, cancellationToken);
    }

    // UPLOADING


    // from local file path

    /// <summary>
    /// Uploads file from specified filepath in local filesystem to Dropbox
    /// </summary>
    /// <param name="localFilePath">Path to local file</param>
    /// <param name="dropboxPath">Upload path on Dropbox</param>
    /// <param name="progressCallback">Progress callback with upload percentage and speed</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel the upload</param>
    /// <returns>Task that produces Metadata object for the uploaded file</returns>
    public async Task<Metadata> UploadFileAsync(string localFilePath, string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) {
        ThrowIfNotAuthenticated();
        return await DropboxSync.Main.TransferManager.UploadFileAsync(localFilePath, dropboxPath, progressCallback, cancellationToken);
    }

    /// <summary>
    /// Uploads file from specified filepath in local filesystem to Dropbox
    /// </summary>
    /// <param name="localFilePath">Path to local file</param>
    /// <param name="dropboxPath">Upload path on Dropbox</param>
    /// <param name="progressCallback">Progress callback with upload percentage and speed</param>
    /// <param name="successCallback">Callback for receiving uploaded file Metadata</param>
    /// <param name="errorCallback">Callback that is triggered if any exception happened</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel the upload</param>
    /// <returns></returns>
    public void UploadFile(string localFilePath, string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                    Action<Metadata> successCallback, Action<Exception> errorCallback, CancellationToken? cancellationToken) {
        ThrowIfNotAuthenticated();

        UploadFileAsync(localFilePath, dropboxPath, progressCallback, cancellationToken).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }

    // from bytes

    /// <summary>
    /// Uploads byte array to Dropbox
    /// </summary>
    /// <param name="bytes">Bytes to upload</param>
    /// <param name="dropboxPath">Upload path on Dropbox</param>
    /// <param name="progressCallback">Progress callback with upload percentage and speed</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel the upload</param>
    /// <returns>Task that produces Metadata object for the uploaded file</returns>
    public async Task<Metadata> UploadFileAsync(byte[] bytes, string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) {
        ThrowIfNotAuthenticated();
        // write bytes to temp location
        var tempPath = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName());
        File.WriteAllBytes(tempPath, bytes);
        var metadata = await DropboxSync.Main.TransferManager.UploadFileAsync(tempPath, dropboxPath, progressCallback, cancellationToken);
        // remove temp file
        File.Delete(tempPath);
        return metadata;
    }

    /// <summary>
    /// Uploads byte array to Dropbox
    /// </summary>
    /// <param name="bytes">Bytes to upload</param>
    /// <param name="dropboxPath">Upload path on Dropbox</param>
    /// <param name="progressCallback">Progress callback with upload percentage and speed</param>    
    /// <param name="successCallback">Callback for receiving uploaded file Metadata</param>
    /// <param name="errorCallback">Callback that is triggered if any exception happened</param>
    /// <param name="cancellationToken">Cancellation token that can be used to cancel the upload</param>
    /// <returns></returns>
    public void UploadFile(byte[] bytes, string dropboxPath, Progress<TransferProgressReport> progressCallback,
                                    Action<Metadata> successCallback, Action<Exception> errorCallback, CancellationToken? cancellationToken) {
        ThrowIfNotAuthenticated();
        UploadFileAsync(bytes, dropboxPath, progressCallback, cancellationToken).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }

    // KEEP SYNCED

    /// <summary>
    /// Keep Dropbox file or folder synced (one-way: from Dropbox to Local cache)
    /// </summary>
    /// <param name="dropboxPath">File or folder path on Dropbox</param>
    /// <param name="syncedCallback">Callback that is triggered after change is synced from Dropbox</param>
    public void KeepSynced(string dropboxPath, Action<EntryChange> syncedCallback) {
        ThrowIfNotAuthenticated();
        _syncManager.KeepSynced(dropboxPath, syncedCallback);
    }

    /// <summary>
    /// Unsubscribe specified callback from getting synced changes (if there will be no callbacks listening then syncing will automatically stop as well)
    /// </summary>
    /// <param name="dropboxPath">File or folder path on Dropbox</param>
    /// <param name="syncedCallback">Callback that you wish to unsubscribe</param>
    public void UnsubscribeFromKeepSyncCallback(string dropboxPath, Action<EntryChange> syncedCallback) {
        ThrowIfNotAuthenticated();
        _syncManager.UnsubscribeFromKeepSyncCallback(dropboxPath, syncedCallback);
    }

    /// <summary>
    /// Stop keeping in sync Dropbox file or folder
    /// </summary>
    /// <param name="dropboxPath">File or folder path on Dropbox</param>
    public void StopKeepingInSync(string dropboxPath) {
        ThrowIfNotAuthenticated();
        _syncManager.StopKeepingInSync(dropboxPath);
    }

    /// <summary>
    /// Checks if currently keeping Dropbox file of folder in sync
    /// </summary>
    /// <param name="dropboxPath">File or folder path on Dropbox</param>
    /// <returns></returns>
    public bool IsKeepingInSync(string dropboxPath) {
        ThrowIfNotAuthenticated();
        return _syncManager != null && _syncManager.IsKeepingInSync(dropboxPath);
    }

    // OPERATIONS

    // create folder

    /// <summary>
    /// Creates folder on Dropbox
    /// </summary>
    /// <param name="dropboxFolderPath">Folder to create</param>
    /// <param name="autorename">Should autorename if conflicting paths?</param>
    /// <returns>Metadata of created folder</returns>
    public async Task<Metadata> CreateFolderAsync(string dropboxFolderPath, bool autorename = false) {
        ThrowIfNotAuthenticated();
        return (await new CreateFolderRequest(new CreateFolderRequestParameters {
            path = dropboxFolderPath,
            autorename = autorename
        }, _config).ExecuteAsync()).metadata;
    }

    /// <summary>
    /// Creates folder on Dropbox
    /// </summary>
    /// <param name="dropboxFolderPath">Folder to create</param>
    /// <param name="successCallback">Callback for receiving created folder Metadata</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <param name="autorename">Should autorename if conflicting paths?</param>
    /// <returns></returns>
    public void CreateFolder(string dropboxFolderPath, Action<Metadata> successCallback,
                                 Action<Exception> errorCallback, bool autorename = false) {
        ThrowIfNotAuthenticated();
        CreateFolderAsync(dropboxFolderPath, autorename).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }



    // move


    /// <summary>
    /// Move file or folder from one path to another
    /// </summary>
    /// <param name="fromDropboxPath">From where to move</param>
    /// <param name="toDropboxPath">Where to move</param>
    /// <param name="autorename">Should autorename if conflicting paths?</param>    
    /// <returns></returns>
    public async Task<Metadata> MoveAsync(string fromDropboxPath, string toDropboxPath, bool autorename = false) {
        ThrowIfNotAuthenticated();
        return (await new MoveRequest(new MoveRequestParameters {
            from_path = fromDropboxPath,
            to_path = toDropboxPath,
            autorename = autorename
        }, _config).ExecuteAsync()).metadata;
    }

    /// <summary>
    /// Move file or folder from one path to another
    /// </summary>
    /// <param name="fromDropboxPath">From where to move</param>
    /// <param name="toDropboxPath">Where to move</param>    
    /// <param name="successCallback">Callback for receiving moved object Metadata</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <param name="autorename">Should autorename if conflicting paths?</param>    
    /// <returns></returns>
    public void Move(string fromDropboxPath, string toDropboxPath,
                            Action<Metadata> successCallback, Action<Exception> errorCallback,
                            bool autorename = false) {
        ThrowIfNotAuthenticated();
        MoveAsync(fromDropboxPath, toDropboxPath, autorename).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    // delete


    /// <summary>
    /// Delete file or folder on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to delete</param>
    /// <returns>Deleted object Metadata</returns>
    public async Task<Metadata> DeleteAsync(string dropboxPath) {
        ThrowIfNotAuthenticated();
        return (await new DeleteRequest(new PathParameters(dropboxPath), _config).ExecuteAsync()).metadata;
    }

    /// <summary>
    /// Delete file or folder on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to delete</param>
    /// <param name="successCallback">Callback for receiving deleted object Metadata</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <returns></returns>
    public void Delete(string dropboxPath, Action<Metadata> successCallback, Action<Exception> errorCallback) {
        ThrowIfNotAuthenticated();
        DeleteAsync(dropboxPath).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    /// <summary>
    /// Create share link
    /// </summary>
    /// <param name="dropboxPath">Path to share</param>
    /// <param name="audience">Who will have access to the shared path</param>
    /// <param name="access">Type of access</param>
    /// <param name="allow_download">Allow or not download capabilities for shared links</param>
    /// <returns>Shared link metadata</returns>

    public async Task<SharedLinkMetadata> CreateSharedLinkWithSettingsAsync(
        string dropboxPath,
        string audience = LinkAudienceParam.PUBLIC,
        string access = RequestedLinkAccessLevelParam.VIEWER,
        bool allow_download = true
    ) {
        ThrowIfNotAuthenticated();
        return (await new CreateSharedLinkRequest(new SharedLinkRequestParameters {
            path = dropboxPath,
            settings = new SharedLinkSettingsParameters {
                audience = audience,
                access = access,
                allow_download = allow_download
            }
        }, _config).ExecuteAsync()).GetMetadata();
    }

    public void CreateSharedLinkWithSettings(
        string DropboxFilePath, 
        Action<SharedLinkMetadata> successCallback, Action<Exception> errorCallback,
        string audience = LinkAudienceParam.PUBLIC,
        string access = RequestedLinkAccessLevelParam.VIEWER,
        bool allow_download = true
    ) {
        ThrowIfNotAuthenticated();
        CreateSharedLinkWithSettingsAsync(DropboxFilePath).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    // get metadata


    /// <summary>
    /// Get Metadata for file or folder on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to file or folder</param>    
    /// <returns>File's or folder's Metadata</returns>
    public async Task<Metadata> GetMetadataAsync(string dropboxPath) {
        ThrowIfNotAuthenticated();
        return (await new GetMetadataRequest(new GetMetadataRequestParameters {
            path = dropboxPath
        }, _config).ExecuteAsync()).GetMetadata();
    }

    /// <summary>
    /// Get Metadata for file or folder on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to file or folder</param>
    /// <param name="successCallback">Callback for receiving file's or folder's Metadata</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <returns></returns>
    public void GetMetadata(string dropboxPath,
                            Action<Metadata> successCallback, Action<Exception> errorCallback) {
        ThrowIfNotAuthenticated();
        GetMetadataAsync(dropboxPath).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    // exists?


    /// <summary>
    /// Checks if file or folder exists on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to file or folder</param>
    /// <returns></returns>
    public async Task<bool> PathExistsAsync(string dropboxPath) {
        ThrowIfNotAuthenticated();
        try {
            await GetMetadataAsync(dropboxPath);
            return true;
        } catch (DropboxNotFoundAPIException) {
            return false;
        }
    }

    /// <summary>
    /// Checks if file or folder exists on Dropbox
    /// </summary>
    /// <param name="dropboxPath">Path to file or folder</param>
    /// <param name="successCallback">Callback for receiving boolean result</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <returns></returns>
    public void PathExists(string dropboxPath, Action<bool> successCallback, Action<Exception> errorCallback) {
        ThrowIfNotAuthenticated();
        PathExistsAsync(dropboxPath).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    // list folder


    /// <summary>
    /// Get contents of the folder on Dropbox
    /// </summary>
    /// <param name="dropboxFolderPath">Path to folder on Dropbox</param>
    /// <param name="recursive">Include all subdirectories recursively?</param>
    /// <returns>List of file's and folder's Metadata - contents on the folder</returns>
    public async Task<List<Metadata>> ListFolderAsync(string dropboxFolderPath, bool recursive = false) {
        ThrowIfNotAuthenticated();
        dropboxFolderPath = Utils.UnifyDropboxPath(dropboxFolderPath);

        var result = new List<Metadata>();

        var listFolderResponse = await new ListFolderRequest(new ListFolderRequestParameters {
            path = dropboxFolderPath,
            recursive = recursive
        }, _config).ExecuteAsync();

        result.AddRange(listFolderResponse.entries);

        bool has_more = listFolderResponse.has_more;
        string cursor = listFolderResponse.cursor;

        while (has_more) {
            // list_folder/continue
            var continueResponse = await new ListFolderContinueRequest(new CursorRequestParameters {
                cursor = cursor
            }, _config).ExecuteAsync();

            result.AddRange(continueResponse.entries);

            has_more = continueResponse.has_more;
            cursor = continueResponse.cursor;
        }

        return result;
    }

    /// <summary>
    /// Get contents of the folder on Dropbox
    /// </summary>
    /// <param name="dropboxFolderPath">Path to folder on Dropbox</param>
    /// <param name="successCallback">Callback for receiving a List of file's and folder's Metadata - contents on the folder</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <param name="recursive">Include all subdirectories recursively?</param>
    /// <returns></returns>
    public void ListFolder(string dropboxFolderPath,
                                    Action<List<Metadata>> successCallback, Action<Exception> errorCallback,
                                    bool recursive = false) {
        ThrowIfNotAuthenticated();
        ListFolderAsync(dropboxFolderPath, recursive).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    // should update?


    /// <summary>
    /// Checks if Dropbox has different version of the file (always returns true if file is not cached locally)
    /// </summary>
    /// <param name="dropboxFilePath">Path to file on Dropbox</param>
    /// <returns></returns>
    public async Task<bool> ShouldUpdateFromDropboxAsync(string dropboxFilePath) {
        ThrowIfNotAuthenticated();
        var metadata = await GetMetadataAsync(dropboxFilePath);
        if (!metadata.IsFile) {
            throw new ArgumentException("Please specify Dropbox file path, not folder.");
        }

        return _cacheManager.ShouldUpdateFileFromDropbox(metadata);
    }

    /// <summary>
    /// Checks if Dropbox has different version of the file (always returns true if file is not cached locally)
    /// </summary>
    /// <param name="dropboxFilePath">Path to file on Dropbox</param>
    /// <param name="successCallback">Callback for receiving boolean result</param>
    /// <param name="errorCallback">Callback for receiving exceptions</param>
    /// <returns></returns>
    public void ShouldUpdateFileFromDropbox(string dropboxFilePath, Action<bool> successCallback, Action<Exception> errorCallback) {
        ThrowIfNotAuthenticated();
        ShouldUpdateFromDropboxAsync(dropboxFilePath).ContinueWith((t) => {
            if (t.Exception != null) {
                errorCallback(t.Exception);
            } else {
                successCallback(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled);
    }


    private void DisposeManagers() {

        if (_transferManger != null) {
            _transferManger.Dispose();
        }
        if (_changesManager != null) {
            _changesManager.Dispose();
        }
        if (_syncManager != null) {
            _syncManager.Dispose();
        }

        _transferManger = null;
        _changesManager = null;
        _syncManager = null;
    }


    // EVENTS

    void OnApplicationQuit() {
        // print("[DropboxSync] Cleanup");        
        DisposeManagers();
    }
}
