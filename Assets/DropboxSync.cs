using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

using DropboxSync.Model;
using DropboxSync.Utils;
using UnityEngine.UI;
using System.IO;
using System.Threading;




namespace DropboxSync {

	public class DropboxSync : MonoBehaviour {

		float DBXChangeForChangesIntervalSeconds = 5;
		string DBXAccessToken = "2TNf3BjlBqAAAAAAAAAADBc1iIKdoEMOI2uig6oNFWtqijlveLRlDHAVDwrhbndr";
		string _PersistentDataPath = null;

		List<Action> MainThreadQueuedActions = new List<Action>();

		void Awake(){
			Initialize();
		}

		// Use this for initialization
		void Start () {

			//var obj = JSON.FromJson<Dictionary<string, int>>("{\"a\": 1}");
			//Debug.Log(obj["a"]);
			//Debug.Log(JSON.ToJson(obj));

			// try {
			// 	TestListFolder();	
			// } catch (Exception ex){
			// 	Debug.LogError(ex.ToString());
			// }

			// GetFolderItemsFlatRecursive("/folder with spaces", onResult: (items) => {
			// 	Debug.Log("Got results: "+items.Count);
			// }, onError: (errorStr) => {
			// 	Debug.LogError("Got error: "+errorStr);
			// });
			
			// GetFolder("/", onResult: (res) => {
			// 	if(!res.error){
					
			// 	}else{
			// 		Debug.LogError(res.errorDescription);
			// 	}
			// });

			// GetFolderItems("/", (res) => {
			// 	if(!res.error){
			// 		Debug.Log("Total files on dropbox: "+res.data.Where(x => x.type == DBXItemType.File).Count().ToString()); 
			// 	}else{
			// 		Debug.LogError(res.errorDescription);
			// 	}
			// }, recursive:true, onProgress:(progress) => {
			// 	if(progress > 0){
			// 		Debug.Log("progress: "+progress.ToString());
			// 	}				
			// });

			//TestGetMetadata();

			// GetFile<string>("/speech_data/voxforge-ru-dataset/_panic_-20110505-quq/LICENSE", onResult: (result) => {
			// 	if(result.error){
			// 		Debug.LogError("Error downloading file: "+result.errorDescription);
			// 	}else{
			// 		Debug.Log("Got string: "+result.data);
			// 	}
			// }, onProgress: (progress) => {
			// 	Debug.Log(string.Format("download progress: {0}%", progress*100));
			// });

			GetFile<JsonObject>("/folder with spaces/second level depth folder/dbx_list_recursive_example.json", onResult: (result) => {
				if(result.error){
					Debug.LogError("Error downloading file: "+result.errorDescription);
				}else{					
					Debug.Log("Got json cursor: "+result.data["cursor"].ToString());
					Debug.Log("Got json cusor back to json: "+JSON.ToJson(result.data));
				}
			}, onProgress: (progress) => {
				Debug.Log(string.Format("download progress: {0}%", progress*100));
			});

			GetFile<JsonArray>("/folder with spaces/second level depth folder/json_array_example.json", onResult: (result) => {
				if(result.error){
					Debug.LogError("Error downloading file: "+result.errorDescription);
				}else{
					Debug.Log("Got json array: "+string.Join(", ", result.data.Select(x => x as string).ToArray()));
					Debug.Log("Got json array back to json: "+JSON.ToJson(result.data));
				}
			}, onProgress: (progress) => {
				Debug.Log(string.Format("download progress: {0}%", progress*100));
			});

			// GetFileMetadata("/folder with spaces/second level depth folder/json_array_example.json", onResult: (res) => {
			// 	if(res.error){
			// 		Debug.LogError(res.errorDescription);
			// 	}else{
			// 		Debug.Log("Got metadata: "+SimpleJson.SerializeObject(res.data));
			// 	}
			// });

			// CacheFile("/folder with spaces/second level depth folder/json_array_example.json", 
			// onSuccess: () => {
			// 	Debug.Log("Cached!");
			// },
			// onError: (errStr) => {
			// 	Debug.LogError(errStr);
			// },
			// onProgress: (progress) => {

			// });

			// CacheFolder("/HelloThereFolder", onSuccess: () => {
			// 	Debug.Log("Cached folder!");
			// }, 
			// onProgress: (progress) => {
			// 	Debug.Log(progress);
			// }, 
			// onError: (errorStr) => {
			// 	Debug.LogError(errorStr);
			// });



			// FolderGetRemoteChanges("/HelloThereFolder", onResult: (res) => {
			// 	if(res.error){
			// 		Debug.LogError(res.errorDescription);
			// 	}else{
			// 		Debug.Log("File changes: "+res.data.Count);
			// 		foreach(var change in res.data){
			// 			Debug.Log(string.Format("{0} - {1}", change.file.path, change.change.ToString()));
			// 		}
			// 	}
			// });

			
			// SubscribeToFolderChanges("/helloThereFolder", (changes) => {
			// 		Debug.Log("File changes: "+changes.Count);
			// 		foreach(var change in changes){
			// 			Debug.Log(string.Format("{0} - {1}", change.file.path, change.change.ToString()));
			// 		}
			// });

			Action<Texture2D> updatePic = (tex) => {
				var rawImage = FindObjectOfType<RawImage>();
				rawImage.texture = tex;
				rawImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)tex.width/tex.height;
			};

			var imageDropbobxPath = "/Meydanprojectsmap_scaled.jpg";
			
			GetFile<Texture2D>(imageDropbobxPath, (res) => {
				if(res.error){
					Debug.LogError(res.errorDescription);
				}else{
					updatePic(res.data);
				}
			}, useCachedIfPossible:false, useCachedIfOffline:false);


			

			
		}
		
		// Update is called once per frame
		void Update () {
			if(Time.unscaledTime - _lastTimeCheckedForChanges > DBXChangeForChangesIntervalSeconds){
				CheckChangesForSubscribedItems();
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


		// MONITORING CHANGES

		float _lastTimeCheckedForChanges = -999999;

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
						if(fileChange.change != DBXFileChangeType.None){
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

		public void GetFile<T>(string dropboxPath, Action<DropboxRequestResult<T>> onResult, Action<float> onProgress = null, bool useCachedIfPossible = false,
		bool useCachedIfOffline = true) where T : class{
			Action<DropboxRequestResult<byte[]>> onResultMiddle = null;

			if(typeof(T) == typeof(string)){
				Log("GetFile: text type");

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
				Log("GetFile: JSON type");

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
				Log("GetFile: Texture2D type");
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

			GetFileAsBytes(dropboxPath, onResultMiddle, onProgress, useCachedIfPossible, useCachedIfOffline);
		}


		public void GetFileAsBytes(string dropboxPath, Action<DropboxRequestResult<byte[]>> onResult, Action<float> onProgress = null, bool useCachedIfPossible = false,
		bool useCachedIfOffline = true){
			Action returnCachedResult = () => {
				var bytes = File.ReadAllBytes(GetPathInCache(dropboxPath));
				onResult(new DropboxRequestResult<byte[]>(bytes));
			};

			// maybe no need to do any remote requests
			if(useCachedIfPossible && IsFileCached(dropboxPath)){	
				Log("GetFile: using cached version without checking for updates");			
				returnCachedResult();
			}else{
				Log("GetFile: check if online");
				// now check if we online
				if(DropboxSyncUtils.IsOnline()){
					Log("GetFile: internet available");
					// check if have updates and load them
					UpdateFileFromRemote(dropboxPath, onSuccess: () => {
						Log("GetFile: file "+dropboxPath+" is latest ver now");
						// return updated cached result
						returnCachedResult();
					}, onProgress: onProgress, onError: (errorStr) => {
						onResult(DropboxRequestResult<byte[]>.Error("Cant get file: "+errorStr));
					});
				}else{
					Log("GetFile: internet not available");

					if(useCachedIfOffline && IsFileCached(dropboxPath)){
						Log("GetFile: cannot check for updates - using cached version");
						returnCachedResult();
					}else{
						// error
						onResult(DropboxRequestResult<byte[]>.Error("GetFile: No internet connection"));
					}
				}
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

		void UpdateFolderFromRemote(string dropboxFolderPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
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

							if(fileChange.change == DBXFileChangeType.Added || fileChange.change == DBXFileChangeType.Modified){
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
							}else if(fileChange.change == DBXFileChangeType.Deleted){
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
			FileGetRemoteChanges(dropboxPath, onResult: (fileChange) => {
								
				
				if(fileChange.change == DBXFileChangeType.Modified || fileChange.change == DBXFileChangeType.Added){
					DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
				}else if(fileChange.change == DBXFileChangeType.Deleted){
					DeleteFileFromCache(dropboxPath);
					onSuccess();
				}else{
					// no changes on remote
					// check if file actually downloaded, not only metadata
					if(IsFileCached(dropboxPath)){
						onSuccess();
					}else{
						DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
					}					
				}
			}, onError: onError, saveChangesInfoLocally:true);									
		}

		void DeleteFileFromCache(string dropboxPath, bool deleteWithMetadata = true){
			var localFilePath = GetPathInCache(dropboxPath);
			if(File.Exists(localFilePath)){
				File.Delete(localFilePath);
			}		

			if(deleteWithMetadata){
				var metadataFilePath = GetMetadataFilePath(dropboxPath);
				if(File.Exists(metadataFilePath)){
					File.Delete(metadataFilePath);
				}	
			}			
		}

		void DownloadToCache (string dropboxPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
				//Log("DownloadToCache");
				DownloadFileBytes(dropboxPath, (res) => {
					if(res.error){
						onError(res.errorDescription);						
					}else{
						var localFilePath = GetPathInCache(dropboxPath);
						Log("Cache folder path: "+CacheFolderPathForToken	);
						Log("Local cached file path: "+localFilePath);					
						

						// make sure containing directory exists
						var fileDirectoryPath = Path.GetDirectoryName(localFilePath);
						Log("Local cached directory path: "+fileDirectoryPath);
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
				var accessTokeFirst5Characters = DBXAccessToken.Substring(0, 5);
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
			Log("Wrote metadata file "+newMetadataFilePath);
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

		public void FolderGetRemoteChanges(string dropboxFolderPath, Action<DropboxRequestResult<List<DBXFileChange>>> onResult, bool saveChangesInfoLocally = false){
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

		public void FileGetRemoteChanges(string dropboxFilePath, Action<DBXFileChange> onResult, Action<string> onError, bool saveChangesInfoLocally = false){
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
							}							
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

							// check if local file has same size
							/*var remoteByteSize = remoteMedatadata.filesize;								
							if(File.Exists(localFilePath)){
								var localByteSize = new FileInfo(localFilePath).Length;
								if(localByteSize != remoteByteSize){
									result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Modified);
								}else{
									result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.None);
								}
							}else{
								result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Added);
							}*/
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

					//Log("onResult "+result.change.ToString());
					onResult(result);
				}				

			});
		}

		
		// GETTING FOLDER STRUCTURE

		public void GetFolder(string path, Action<DropboxRequestResult<DBXFolder>> onResult, Action<float> onProgress = null){
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
				Log("Got reponse: "+jsonStr);

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

				//Log("Current results: "+currentResults.Count);
				//Log("Has more: "+root["has_more"].ToString());

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
			try {
				using (var client = new WebClient()){				
					client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
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
			try {
				using (var client = new WebClient()){				
					client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);					
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

		// void TestListFolder(){
		// 	using (var client = new WebClient()){
				
		// 		var url = "https://api.dropboxapi.com/2/files/list_folder";
		// 		client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
		// 		client.Headers.Set("Content-Type", "application/json");				

		// 		var par = new DropboxListFolderRequestParams{path="/asdsa"};
			

		// 		var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
		// 		var respStr = Encoding.UTF8.GetString(respBytes);
				
		// 		Log(respStr);

		// 		var root = SimpleJson.DeserializeObject(respStr) as JsonObject;
		// 		var entries = root["entries"] as JsonArray;

		// 		var item = DBXFolder.FromDropboxJsonObject(entries[0] as JsonObject);

		// 		Log(JsonUtility.ToJson(item, prettyPrint:true));
				
		// 	}
		// }

		// void TestGetMetadata(){
		// 	using (var client = new WebClient()){
		// 		var url = "https://api.dropboxapi.com/2/files/get_metadata";
		// 		client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
		// 		client.Headers.Set("Content-Type", "application/json");				

		// 		var par = new DropboxGetMetadataRequestParams("/folder with spaces");
			

		// 		var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
		// 		var respStr = Encoding.UTF8.GetString(respBytes);
				
		// 		Log(respStr);
		// 	}
		// }


		// THREADING

		void QueueOnMainThread(Action a){
			lock(MainThreadQueuedActions){
				MainThreadQueuedActions.Add(a);
			}
		}

		// LOGGING

		void Log(string message){
			Debug.Log("[DropboxSync] "+message);
		}

		void LogWarning(string message){
			Debug.LogWarning("[DropboxSync] "+message);
		}

		void LogError(string message){
			Debug.LogError("[DropboxSync] "+message);
		}



		// EVENTS
	}

}