using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

using DBXSync.Model;
using DBXSync.Utils;
using UnityEngine.UI;
using System.IO;
using System.Threading;



namespace DBXSync {

	public enum DropboxSyncLogLevel {
		Debug = 0,
		Warnings = 1,
		Errors = 2
	}


	public class DropboxSync : MonoBehaviour {

		public static DropboxSyncLogLevel LOG_LEVEL = DropboxSyncLogLevel.Warnings;

		float DBXChangeForChangesIntervalSeconds = 5;
		public string DropboxAccessToken = "<YOUR ACCESS TOKEN>";
		string _PersistentDataPath = null;


		public static DropboxSync Main {
			get {
				var instance = FindObjectOfType<DropboxSync>();
				if(instance != null){
					return instance;
				}else{
					Debug.LogError("DropboxSync script wasn't found on scene.");
					return null;
				}
			}
		}
		

		void Awake(){
			Initialize();
		}		

		
		// internet connection
		bool _noIternetWarningDisplayed = false;
		List<Action> OnInternetRecoverOnceCallbacks = new List<Action>();
		

		List<Action> MainThreadQueuedActions = new List<Action>();
		
		float _lastTimeCheckedForChanges = -999999;

		void Update () {
			if(Time.unscaledTime - _lastTimeCheckedForChanges > DBXChangeForChangesIntervalSeconds){
				DropboxSyncUtils.IsOnlineAsync((isOnline) => {
					if(isOnline){
						if(_noIternetWarningDisplayed){
							Log("Internet connection recovered");

							foreach(var a in OnInternetRecoverOnceCallbacks){
								a();
							}
							OnInternetRecoverOnceCallbacks.Clear();
							
						}
						_noIternetWarningDisplayed = false;					
						
						CheckChangesForSubscribedItems();
					}else{
						if(!_noIternetWarningDisplayed){
							LogWarning("No internet connection - can't check dropbox updates");
						}					
						_noIternetWarningDisplayed = true;
					}
				});
				
				_lastTimeCheckedForChanges = Time.unscaledTime;
			}

			lock(MainThreadQueuedActions){
				var _currentActions = new List<Action>(MainThreadQueuedActions);
				MainThreadQueuedActions.Clear();
				foreach(var a in _currentActions){
					if(a != null){
						a();
					}						
				}
			}
		}

		// METHODS

		void Initialize(){
			_PersistentDataPath = Application.persistentDataPath;			
		}

		
		Dictionary<DBXItem, List<Action<List<DBXFileChange>>>> OnChangeCallbacksDict = new Dictionary<DBXItem, List<Action<List<DBXFileChange>>>>();
		void CheckChangesForSubscribedItems(){
			if(OnChangeCallbacksDict.Count == 0){
				return;
			}

			Log("CheckChangesForSubscribedItems ("+OnChangeCallbacksDict.Count.ToString()+")");
			

			foreach(var kv in OnChangeCallbacksDict){
				var item = kv.Key;
				var callbacks = kv.Value;
				
				switch(item.type){
					case DBXItemType.File:
					FileGetRemoteChanges(item.path, (fileChange) => {
						if(fileChange.changeType != DBXFileChangeType.None){
							foreach(var cb in callbacks){
								cb(new List<DBXFileChange>(){fileChange});
							}
						}						
					}, (errorStr) => {
						LogError("Failed to check file changes: "+errorStr);
					}, saveChangesInfoLocally: true);
					break;
					case DBXItemType.Folder:
					FolderGetRemoteChanges(item.path, (res) => {
						if(!res.error){
							if(res.data.Count > 0){
								foreach(var cb in callbacks){
									cb(res.data);
								}
							}
								
						}else{
							LogError("Failed to check folder changes: "+res.errorDescription);
						}
					}, saveChangesInfoLocally: true);
					break;
					default:
					break;
				}
			}
		}

		void SubscribeToInternetRecoverOnce(Action a){			
			OnInternetRecoverOnceCallbacks.Add(a);
		}

		public void SubscribeToFileChanges(string dropboxFilePath, Action<DBXFileChange> onChange){
			var item = new DBXFile(dropboxFilePath);
			SubscribeToChanges(item, (changes) => {
				onChange(changes[0]);
			});
		}

		public void SubscribeToFolderChanges(string dropboxFilePath, Action<List<DBXFileChange>> onChange){
			var item = new DBXFolder(dropboxFilePath);
			SubscribeToChanges(item, onChange);
		}

		void SubscribeToChanges(DBXItem item, Action<List<DBXFileChange>> onChange){
			if(!OnChangeCallbacksDict.ContainsKey(item)){
				// create new list for callbacks
				OnChangeCallbacksDict.Add(item, new List<Action<List<DBXFileChange>>>());
			}

			OnChangeCallbacksDict[item].Add(onChange);			
		}
			

		public void UnsubscribeAllForPath(string dropboxPath){
			dropboxPath = DropboxSyncUtils.NormalizePath(dropboxPath);

			var removeKeys = OnChangeCallbacksDict.Where(p => p.Key.path == dropboxPath).Select(p => p.Key).ToList();
			foreach(var k in removeKeys){
				OnChangeCallbacksDict.Remove(k);
			}
		}

		public void UnsubscribeFromChanges(string dropboxPath, Action<List<DBXFileChange>> onChange){
			dropboxPath = DropboxSyncUtils.NormalizePath(dropboxPath);

			var item = OnChangeCallbacksDict.Where(p => p.Key.path == dropboxPath).Select(p => p.Key).FirstOrDefault();
			if(item != null){
				OnChangeCallbacksDict[item].Remove(onChange);
			}
		}


		// GETTING FILE/FOLDER

		public void GetFile<T>(string dropboxPath, Action<DropboxRequestResult<T>> onResult, Action<float> onProgress = null, bool useCachedFirst = false,
		bool useCachedIfOffline = true, bool receiveUpdates = false) where T : class{
			Action<DropboxRequestResult<byte[]>> onResultMiddle = null;

			if(typeof(T) == typeof(string)){
				//Log("GetFile: text type");

				// TEXT DATA
				onResultMiddle = (res) => {		
					if(res.error){
						onResult(DropboxRequestResult<T>.Error(res.errorDescription));
					}else{			
						onResult(new DropboxRequestResult<T>(DropboxSyncUtils.GetAudtoDetectedEncodingStringFromBytes(res.data) as T));										
					}
				};				
			}
			else if(typeof(T) == typeof(JsonObject) || typeof(T) == typeof(JsonArray)){
				//Log("GetFile: JSON type");

				// JSON OBJECT/ARRAY
				onResultMiddle = (res) => {					
					if(res.error){
						onResult(DropboxRequestResult<T>.Error(res.errorDescription));
					}else{
						onResult(new DropboxRequestResult<T>(JSON.FromJson<T>(
							DropboxSyncUtils.GetAudtoDetectedEncodingStringFromBytes(res.data)
						)));
					}
				};	
			}
			else if(typeof(T) == typeof(Texture2D)){
				//Log("GetFile: Texture2D type");
				// IMAGE DATA
				onResultMiddle = (res) => {				
					if(res.error){
						onResult(DropboxRequestResult<T>.Error(res.errorDescription));
					}else{
						onResult(new DropboxRequestResult<T>(DropboxSyncUtils.LoadImageToTexture2D(res.data) as T));
					}
				};	
			}
			else{
				onResult(DropboxRequestResult<T>.Error(string.Format("Dont have a mapping byte[] -> {0}. Type {0} is not supported.", typeof(T).ToString())));
				return;
			}

			GetFileAsBytes(dropboxPath, onResultMiddle, onProgress, useCachedFirst, useCachedIfOffline, receiveUpdates);
		}

		public void GetFileAsLocalCachedPath(string dropboxPath, Action<DropboxRequestResult<string>> onResult, Action<float> onProgress = null, bool useCachedFirst = false,
		bool useCachedIfOffline = true, bool receiveUpdates = false){
			Action<DropboxRequestResult<byte[]>> onResultMiddle = (res) => {					
				if(res.error){
					onResult(DropboxRequestResult<string>.Error(res.errorDescription));
				}else{
					if(res.data != null){
						onResult(new DropboxRequestResult<string>(GetPathInCache(dropboxPath)));
					}else{
						onResult(new DropboxRequestResult<string>(null));
					}					
				}
			};	

			GetFileAsBytes(dropboxPath, onResultMiddle, onProgress, useCachedFirst, useCachedIfOffline, receiveUpdates);
		}

		public void GetFileAsBytes(string dropboxPath, Action<DropboxRequestResult<byte[]>> onResult, Action<float> onProgress = null, bool useCachedFirst = false,
		bool useCachedIfOffline = true, bool receiveUpdates = false){
			if(DropboxSyncUtils.IsBadDropboxPath(dropboxPath)){
				onResult(DropboxRequestResult<byte[]>.Error("Cant get file: bad path "+dropboxPath));
				return;
			}

			Action returnCachedResult = () => {
				var cachedFilePath = GetPathInCache(dropboxPath);

				if(File.Exists(cachedFilePath)){
					var bytes = File.ReadAllBytes(cachedFilePath);
					onResult(new DropboxRequestResult<byte[]>(bytes));
				}else{
					Log("cache doesnt have file");
					onResult(new DropboxRequestResult<byte[]>(null));
				}				
			};

			Action subscribeToUpdatesAction = () => {
				SubscribeToFileChanges(dropboxPath, (fileChange) => {					
					UpdateFileFromRemote(dropboxPath, onSuccess: () => {							
						// return updated cached result
						returnCachedResult();
					}, onProgress: onProgress, onError: (errorStr) => {
						onResult(DropboxRequestResult<byte[]>.Error("Cant get file: "+errorStr));
					});					
				});
			};

			// maybe no need to do any remote requests
			if((useCachedFirst) && IsFileCached(dropboxPath)){	
				Log("GetFile: using cached version");			
				returnCachedResult();

				if(receiveUpdates){
					subscribeToUpdatesAction();
				}
			}else{
				//Log("GetFile: check if online");
				// now check if we online
				DropboxSyncUtils.IsOnlineAsync((isOnline) => {
					if(isOnline){
					Log("GetFile: internet available");
					// check if have updates and load them
					UpdateFileFromRemote(dropboxPath, onSuccess: () => {
						Log("GetFile: file "+dropboxPath+" is latest ver now");
						// return updated cached result
						returnCachedResult();

						if(receiveUpdates){
							subscribeToUpdatesAction();
						}
					}, onProgress: onProgress, onError: (errorStr) => {
						//Log("error");
						onResult(DropboxRequestResult<byte[]>.Error("Cant get file: "+errorStr));

						if(receiveUpdates){
							subscribeToUpdatesAction();
						}
					});
					}else{
						Log("GetFile: internet not available");

						if(useCachedIfOffline && IsFileCached(dropboxPath)){
							Log("GetFile: cannot check for updates - using cached version");
							QueueOnMainThread(() => {
								returnCachedResult();
							});
							
							if(receiveUpdates){
								subscribeToUpdatesAction();
							}
						}else{
							if(receiveUpdates){
								// try again when internet recovers
								SubscribeToInternetRecoverOnce(() => {
									GetFileAsBytes(dropboxPath, onResult, onProgress, useCachedFirst, useCachedIfOffline, receiveUpdates);								
								});

								subscribeToUpdatesAction();
							}else{
								// error
								onResult(DropboxRequestResult<byte[]>.Error("GetFile: No internet connection"));	
							}						
						}
					}
				});
			}

			
		}

		void DownloadFileBytes(string dropboxPath, Action<DropboxFileDownloadRequestResult<byte[]>> onResult, Action<float> onProgress = null){
			var prms = new DropboxDownloadFileRequestParams(dropboxPath);
			MakeDropboxDownloadRequest("https://content.dropboxapi.com/2/files/download", prms,
			onResponse: (fileMetadata, data) => {
				onResult(new DropboxFileDownloadRequestResult<byte[]>(data, fileMetadata));
			},
			onProgress: onProgress,
			onWebError: (webErrorStr) => {
				onResult(DropboxFileDownloadRequestResult<byte[]>.Error(webErrorStr));
			});
		}

		public void GetFileMetadata(string dropboxPath, Action<DropboxRequestResult<DBXFile>> onResult){
			var prms = new DropboxGetMetadataRequestParams(dropboxPath);

			//Log("GetFileMetadata");
			MakeDropboxRequest("https://api.dropboxapi.com/2/files/get_metadata", prms, 
			onResponse: (jsonStr) => {
				var dict = JSON.FromJson<Dictionary<string, object>>(jsonStr);				
				var fileMetadata = DBXFile.FromDropboxDictionary(dict);
				onResult(new DropboxRequestResult<DBXFile>(fileMetadata));
			},
			onProgress:null,
			onWebError: (errStr) => {
				//Log("GetFileMetadata:onWebError");
				onResult(DropboxRequestResult<DBXFile>.Error(errStr));
			});
		}


		// CACHING

		public void SyncFolderFromDropbox(string dropboxFolderPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
			FolderGetRemoteChanges(dropboxFolderPath, onResult:(res) => {
				if(res.error){
					onError(res.errorDescription);
				}else{
					var fileChanges = res.data;

					var thread = new Thread(() => {
						var i = 0;
						foreach(DBXFileChange fileChange in fileChanges){
							var finishedCachingFile = false;
							var wasError = false;

							if(fileChange.changeType == DBXFileChangeType.Added || fileChange.changeType == DBXFileChangeType.Modified){
								DownloadToCache(fileChange.file.path, onSuccess: () => {
									finishedCachingFile = true;
								}, 
								onProgress: (progress) => {
									onProgress(((float)i + progress)/fileChanges.Count);
								},
								onError: (errorStr) => {
									onError(errorStr);
									wasError = true;
									finishedCachingFile = true;
								});
							}else if(fileChange.changeType == DBXFileChangeType.Deleted){
								// file deleted on remote
								DeleteFileFromCache(fileChange.file.path);
								finishedCachingFile = true;
							}else{
								// no changes
								finishedCachingFile = true;
							}							

							while(!finishedCachingFile){
								Thread.Sleep(200);
							}

							if(wasError){							
								break;
							}

							i++;
						}

						onSuccess();
					});

					thread.IsBackground = true;
					thread.Start();
					thread.Join();
				}			
			});
		}

		void UpdateFileFromRemote(DBXFile dropboxFile, Action onSuccess, Action<float> onProgress, Action<string> onError){
			UpdateFileFromRemote(dropboxFile.path, onSuccess, onProgress, onError);
		}

		void UpdateFileFromRemote(string dropboxPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
			Log("UpdateFileFromRemote");
			FileGetRemoteChanges(dropboxPath, onResult: (fileChange) => {
				Log("FileGetRemoteChanges:onResult");
				
				if(fileChange.changeType == DBXFileChangeType.Modified || fileChange.changeType == DBXFileChangeType.Added){
					DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
				}else if(fileChange.changeType == DBXFileChangeType.Deleted){
					DeleteFileFromCache(dropboxPath);
					onSuccess();
				}else{
					// no changes on remote
					if(!GetLocalMetadataForFile(dropboxPath).deletedOnRemote){
						// check if file actually downloaded, not only metadata
						if(IsFileCached(dropboxPath)){
							onSuccess();
						}else{
							DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
						}
					}else{
						DeleteFileFromCache(dropboxPath);
						// no changes, file is deleted locally and on remote - synced
						onSuccess();
					}									
				}
			}, onError: onError, saveChangesInfoLocally:true);									
		}

		void DeleteFileFromCache(string dropboxPath){
			var localFilePath = GetPathInCache(dropboxPath);
			if(File.Exists(localFilePath)){
				File.Delete(localFilePath);
			}		
		}

		void DownloadToCache (string dropboxPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
				//Log("DownloadToCache");
				DownloadFileBytes(dropboxPath, (res) => {
					if(res.error){
						onError(res.errorDescription);						
					}else{
						var localFilePath = GetPathInCache(dropboxPath);
						//Log("Cache folder path: "+CacheFolderPathForToken	);
						//Log("Local cached file path: "+localFilePath);

						// make sure containing directory exists
						var fileDirectoryPath = Path.GetDirectoryName(localFilePath);
						//Log("Local cached directory path: "+fileDirectoryPath);
						Directory.CreateDirectory(fileDirectoryPath);

						File.WriteAllBytes(localFilePath, res.data);						

						// write metadata
						SaveFileMetadata(res.fileMetadata);

						onSuccess();
					}
				}, onProgress: onProgress);
		}

		bool IsFileCached(string dropboxPath){
			var metadata = GetLocalMetadataForFile(dropboxPath);
			var localFilePath = GetPathInCache(dropboxPath);
			if(metadata != null){
				if(File.Exists(localFilePath)){
					return metadata.filesize == new FileInfo(localFilePath).Length;
				}
			}
			return false;
		}

		string GetPathInCache(string dropboxPath){
			var relativeDropboxPath = dropboxPath.Substring(1);			
			if(relativeDropboxPath.Last() == '/'){
				relativeDropboxPath = relativeDropboxPath.Substring(relativeDropboxPath.Length-1);
			}
			return Path.Combine(CacheFolderPathForToken, relativeDropboxPath);
		}	

		string GetMetadataFilePath(string dropboxPath){
			return GetPathInCache(dropboxPath)+".dbxsync";
		}

		string CacheFolderPathForToken {
			get {
				DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);

				var accessTokeFirst5Characters = DropboxAccessToken.Substring(0, 5);
				return Path.Combine(_PersistentDataPath, accessTokeFirst5Characters);
			}		
		}

		void SaveFileMetadata(DBXFile fileMetadata){		
			
			var localFilePath = GetPathInCache(fileMetadata.path);		
			
			// make sure containing directory exists
			var fileDirectoryPath = Path.GetDirectoryName(localFilePath);
			//Log("Local cached directory path: "+fileDirectoryPath);
			Directory.CreateDirectory(fileDirectoryPath);

			// write metadata to separate file near
			var newMetadataFilePath = GetMetadataFilePath(fileMetadata.path);
			File.WriteAllText(newMetadataFilePath, JsonUtility.ToJson(fileMetadata));
			//Log("Wrote metadata file "+newMetadataFilePath);
		}

		DBXFile GetLocalMetadataForFile(string dropboxFilePath){
			var metadataFilePath = GetMetadataFilePath(dropboxFilePath);
			return ParseLocalMetadata(metadataFilePath);
		}

		DBXFile ParseLocalMetadata(string localMetadataPath){
			if(File.Exists(localMetadataPath)){
				// get local content hash
				var fileJsonStr = File.ReadAllText(localMetadataPath);				
				
				try {
					return JsonUtility.FromJson<DBXFile>(fileJsonStr);					
				}catch{
					return null;
				}		
			}
			return null;
		}

		// GETTING CHANGES

		void FolderGetRemoteChanges(string dropboxFolderPath, Action<DropboxRequestResult<List<DBXFileChange>>> onResult, bool saveChangesInfoLocally = false){
			GetFolderItems(dropboxFolderPath, 
			onResult: (res) => {
				if(res.error){
					onResult(DropboxRequestResult<List<DBXFileChange>>.Error(res.errorDescription));
				}else{
					var fileChanges = new List<DBXFileChange>();

					foreach(DBXFile remoteMetadata in res.data.Where(x => x.type == DBXItemType.File)){
						var localMetadata = GetLocalMetadataForFile(remoteMetadata.path);
						if(localMetadata != null && !localMetadata.deletedOnRemote){
							if(localMetadata.contentHash != remoteMetadata.contentHash){
								fileChanges.Add(new DBXFileChange(remoteMetadata, DBXFileChangeType.Modified));						
							}
						}else{
							// no local metadata for this remote path - new object
							fileChanges.Add(new DBXFileChange(remoteMetadata, DBXFileChangeType.Added));
						}						
					}

					// find other local files which were not in remote response (find deleted on remote files)
					var processedDropboxFilePaths = res.data.Where(x => x.type == DBXItemType.File).Select(x => x.path).ToList();

					//Log("Find all metadata paths");
					var localDirectoryPath = GetPathInCache(dropboxFolderPath);
					if(Directory.Exists(localDirectoryPath)){
						foreach (string localMetadataFilePath in Directory.GetFiles(localDirectoryPath, "*.dbxsync", SearchOption.AllDirectories)){							
							var metadata = ParseLocalMetadata(localMetadataFilePath);
							var dropboxPath = metadata.path;
							if(!processedDropboxFilePaths.Contains(dropboxPath) && !metadata.deletedOnRemote){
								// wasnt in remote data - means removed
								fileChanges.Add(new DBXFileChange(DBXFile.DeletedOnRemote(dropboxPath), DBXFileChangeType.Deleted));
							}
						}
					}

					if(saveChangesInfoLocally){
						foreach(var fc in fileChanges){
							SaveFileMetadata(fc.file);
						}
					}

					onResult(new DropboxRequestResult<List<DBXFileChange>>(fileChanges));
				}
			}, recursive:true, onProgress:null);
		}

		void FileGetRemoteChanges(string dropboxFilePath, Action<DBXFileChange> onResult, Action<string> onError, bool saveChangesInfoLocally = false){
			var localFilePath = GetPathInCache(dropboxFilePath);
			var metadataFilePath = GetMetadataFilePath(dropboxFilePath);			

			var localMetadata = GetLocalMetadataForFile(dropboxFilePath);
				
			// request for metadata to get remote content hash
			//Log("Getting metadata");
			GetFileMetadata(dropboxFilePath, onResult: (res) => {
				DBXFileChange result = null;

				if(res.error){							
					if (res.errorDescription.Contains("not_found")){
						//Log("file not found");
						// file was deleted or moved

						// if we knew about this file before
						if(localMetadata != null){
							// if we didnt know that it was removed
							if(!localMetadata.deletedOnRemote){
								result = new DBXFileChange(DBXFile.DeletedOnRemote(dropboxFilePath), DBXFileChangeType.Deleted);
							}else{
								// no change								
								result = new DBXFileChange(localMetadata, DBXFileChangeType.None);
							}
						}else{
							onError("File "+dropboxFilePath+" not found on DropBox");
						}
						
					}else{
						onError(res.errorDescription);
						return;
					}									
				}else{
					//Log("Got metadata");
					var remoteMedatadata = res.data;

					if(localMetadata != null && !localMetadata.deletedOnRemote){
						// get local content hash				
						var local_content_hash = localMetadata.contentHash;

						var remote_content_hash = remoteMedatadata.contentHash;

						if(local_content_hash != remote_content_hash){
							result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Modified);
						}else{
							result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.None);						
						}	
					}else{						
						// metadata file doesnt exist
						// TODO: check maybe file itself exists and right version, then just create metadata file - no need to redownload file itself
						result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Added);
					}
				}

				// if no error
				if(result != null){
					if(saveChangesInfoLocally){			

						SaveFileMetadata(result.file);
					}
					
					onResult(result);
				}
			});
		}

		
		// GETTING FOLDER STRUCTURE

		public void GetFolderStructure(string path, Action<DropboxRequestResult<DBXFolder>> onResult, Action<float> onProgress = null){
			path = DropboxSyncUtils.NormalizePath(path);

			_GetFolderItemsFlat(path, onResult: (items) => {
				DBXFolder rootFolder = null;

				// get root folder
				if(path == "/"){
					rootFolder = new DBXFolder{id="", path="/", name="", items = new List<DBXItem>()};			
				}else{
					rootFolder = items.Where(x => x.path == path).First() as DBXFolder;			
				}
				// squash flat results
				rootFolder = BuildStructureFromPool(rootFolder, items);

				onResult(new DropboxRequestResult<DBXFolder>(rootFolder));
			},
			onProgress:onProgress,
			onError: (errorStr) => {
				onResult(DropboxRequestResult<DBXFolder>.Error(errorStr));
			}, recursive: true);
		}

		public void GetFolderItems(string path, Action<DropboxRequestResult<List<DBXItem>>> onResult, bool recursive = false, Action<float> onProgress = null){
			_GetFolderItemsFlat(path, onResult: (items) => {		
				onResult(new DropboxRequestResult<List<DBXItem>>(items));
			},
			onProgress:onProgress,
			onError: (errorStr) => {
				onResult(DropboxRequestResult<List<DBXItem>>.Error(errorStr));
			}, recursive: recursive);
		}

		DBXFolder BuildStructureFromPool(DBXFolder rootFolder, List<DBXItem> pool){		
			foreach(var poolItem in pool){
				// if item is immediate child of rootFolder
				if(DropboxSyncUtils.IsPathImmediateChildOfFolder(rootFolder.path, poolItem.path)){
					// add poolItem to folder children
					if(poolItem.type == DBXItemType.Folder){
						//Debug.Log("Build structure recursive");
						rootFolder.items.Add(BuildStructureFromPool(poolItem as DBXFolder, pool));	
					}else{
						rootFolder.items.Add(poolItem);	
					}				
					//Debug.Log("Added child "+poolItem.path);			
				}
			}

			return rootFolder;
		}

		void _GetFolderItemsFlat(string folderPath, Action<List<DBXItem>> onResult, Action<float> onProgress, Action<string> onError, bool recursive = false, string requestCursor = null, List<DBXItem> currentResults = null){
			folderPath = DropboxSyncUtils.NormalizePath(folderPath);

			if(folderPath == "/"){
				folderPath = ""; // dropbox error fix
			}

			string url;
			DropboxRequestParams prms;
			if(requestCursor == null){
				// first request
				currentResults = new List<DBXItem>();
				url = "https://api.dropboxapi.com/2/files/list_folder";
				prms = new DropboxListFolderRequestParams{path=folderPath, recursive=recursive};
			}else{
				// have cursor to continue list
				url = "https://api.dropboxapi.com/2/files/list_folder/continue";
				prms = new DropboxContinueWithCursorRequestParams(requestCursor);
			}
			
			MakeDropboxRequest(url, prms, onResponse: (jsonStr) => {
				//Log("Got reponse: "+jsonStr);

				Dictionary<string, object> root = null;
				try {
					root = JSON.FromJson<Dictionary<string, object>>(jsonStr);
				}catch(Exception ex){
					onError(ex.Message);
					return;
				}

				var entries = root["entries"] as List<object>;
				foreach(Dictionary<string, object> entry in entries){
					if(entry[".tag"].ToString() == "file"){
						currentResults.Add(DBXFile.FromDropboxDictionary(entry));
					}else if(entry[".tag"].ToString() == "folder"){
						currentResults.Add(DBXFolder.FromDropboxDictionary(entry));
					}else{
						onError("Unknown entry tag "+entry[".tag".ToString()]);
						return;
					}
				}

				if((bool)root["has_more"]){
					// recursion
					_GetFolderItemsFlat(folderPath, onResult, onProgress, onError, recursive: recursive,
					requestCursor:root["cursor"].ToString(), 
					currentResults: currentResults);
				}else{
					// done
					onResult(currentResults);
				}

			}, onProgress: onProgress,
				onWebError: (webErrorStr) => {
				//LogError("Got web err: "+webErrorStr);
				onError(webErrorStr);
			});
		}


		// BASE REQUESTS

		void MakeDropboxRequest<T>(string url, T parametersObject, Action<string> onResponse, Action<float> onProgress, Action<string> onWebError) where T : DropboxRequestParams{
			MakeDropboxRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}	

		void MakeDropboxRequest(string url, string jsonParameters, Action<string> onResponse, Action<float> onProgress, Action<string> onWebError){
			DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);

			if(!DropboxSyncUtils.IsOnline()){
				onWebError("No internet connection");
			}

			try {
				using (var client = new WebClient()){				
					client.Headers.Set("Authorization", "Bearer "+DropboxAccessToken);
					client.Headers.Set("Content-Type", "application/json");
					
					client.DownloadProgressChanged += (s, e) => {
						if(onProgress != null){
							//Log(string.Format("Downloaded {0} bytes out of {1}", e.BytesReceived, e.TotalBytesToReceive));
							if(e.TotalBytesToReceive != -1){
								// if download size in known from server
								QueueOnMainThread(() => {
									onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
								});
							}else{
								// return progress is going but unknown
								QueueOnMainThread(() => {
									onProgress(-1);
								});
							}
						}						
					};

					client.UploadDataCompleted += (s, e) => {						

						if(e.Error != null){
							if(e.Error is WebException){
								var webex = e.Error as WebException;
								var stream = webex.Response.GetResponseStream();
								var reader = new StreamReader(stream);
								var responseStr = reader.ReadToEnd();
								Log(responseStr);

								try{								
									var dict = JSON.FromJson<Dictionary<string, object>>(responseStr);
									var errorSummary = dict["error_summary"].ToString();								
									QueueOnMainThread(() => {
										onWebError(errorSummary);
									});
								}catch{
									QueueOnMainThread(() => {
										onWebError(e.Error.Message);
									});
								}
							}else{
								QueueOnMainThread(() => {
									onWebError(e.Error.Message);
								});
							}

						}else{
							var respStr = Encoding.UTF8.GetString(e.Result);
							QueueOnMainThread(() => {								
								onResponse(respStr);
							});
						}
					};

					var uri = new Uri(url);
					//Log("MakeDropboxRequest:client.UploadDataAsync");				
					client.UploadDataAsync(uri, "POST", Encoding.Default.GetBytes(jsonParameters));						
				}
			} catch (Exception ex){
				//onWebError(ex.Message);
				//Log("caught exeption");
				onWebError(ex.Message);
				//Log(ex.Response.ToString());
			}
		}

		void MakeDropboxDownloadRequest<T>(string url, T parametersObject, Action<DBXFile, byte[]> onResponse, Action<float> onProgress, Action<string> onWebError) where T : DropboxRequestParams{
			MakeDropboxDownloadRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}

		void MakeDropboxDownloadRequest(string url, string jsonParameters, Action<DBXFile, byte[]> onResponse, Action<float> onProgress, Action<string> onWebError){
			DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);

			if(!DropboxSyncUtils.IsOnline()){
				onWebError("No internet connection");
			}

			try {
				using (var client = new WebClient()){				
					client.Headers.Set("Authorization", "Bearer "+DropboxAccessToken);					
					client.Headers.Set("Dropbox-API-Arg", jsonParameters);
					
					client.DownloadProgressChanged += (s, e) => {
						
						if(onProgress != null){
							//Log(string.Format("Downloaded {0} bytes out of {1} ({2}%)", e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage));
							if(e.TotalBytesToReceive != -1){
								// if download size in known from server
								QueueOnMainThread(() => {
									onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
								});
							}else{
								// return progress is going but unknown
								QueueOnMainThread(() => {
									onProgress(-1);
								});
							}
						}						
					};

					client.DownloadDataCompleted += (s, e) => {
						if(e.Error != null){
							if(e.Error is WebException){
								var webex = e.Error as WebException;
								var stream = webex.Response.GetResponseStream();
								var reader = new StreamReader(stream);
								var responseStr = reader.ReadToEnd();
								Log(responseStr);

								try{								
									var dict = JSON.FromJson<Dictionary<string, object>>(responseStr);
									var errorSummary = dict["error_summary"].ToString();	
									QueueOnMainThread(() => {							
										onWebError(errorSummary);
									});
								}catch{
									QueueOnMainThread(() => {
										onWebError(e.Error.Message);
									});
								}
							}else{
								QueueOnMainThread(() => {
									onWebError(e.Error.Message);
								});
							}
						}else if(e.Cancelled){
							QueueOnMainThread(() => {
								onWebError("Download was cancelled.");
							});
						}else{
							//var respStr = Encoding.UTF8.GetString(e.Result);
							var metadataJsonStr = client.ResponseHeaders["Dropbox-API-Result"].ToString();
							Log(metadataJsonStr);
							var dict = JSON.FromJson<Dictionary<string, object>>(metadataJsonStr);
							var fileMetadata = DBXFile.FromDropboxDictionary(dict);

							QueueOnMainThread(() => {
								onResponse(fileMetadata, e.Result);
							});							
						}
					};

					var uri = new Uri(url);
					client.DownloadDataAsync(uri);
				}
			} catch (WebException ex){
				QueueOnMainThread(() => {
					onWebError(ex.Message);
				});
			}
		}

		// THREADING

		void QueueOnMainThread(Action a){
			lock(MainThreadQueuedActions){
				MainThreadQueuedActions.Add(a);
			}
		}

		// LOGGING

		void Log(string message){
			if(LOG_LEVEL <= DropboxSyncLogLevel.Debug)
				Debug.Log("[DropboxSync] "+message);
		}

		void LogWarning(string message){
			if(LOG_LEVEL <= DropboxSyncLogLevel.Warnings)
				Debug.LogWarning("[DropboxSync] "+message);
		}

		void LogError(string message){
			if(LOG_LEVEL <= DropboxSyncLogLevel.Errors)
				Debug.LogError("[DropboxSync] "+message);
		}

	} // class
} // namespace
