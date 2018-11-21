// DropboxSync v1.1
// Created by George Fedoseev 2018

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
	public partial class DropboxSync: MonoBehaviour {

		// GETTING FILE

		public void GetFile<T>(string dropboxPath, Action<DropboxRequestResult<T>> onResult, Action<float> onProgress = null, bool useCachedFirst = false,
		bool useCachedIfOffline = true, bool receiveUpdates = false) where T : class{
			Action<DropboxRequestResult<byte[]>> onResultMiddle = null;

			if(typeof(T) == typeof(string)){
				//Log("GetFile: text type");

				// TEXT DATA
				onResultMiddle = (res) => {		
					if(res.error || res.data == null){
						onResult(DropboxRequestResult<T>.Error(res.errorDescription));
					}else{
						onResult(new DropboxRequestResult<T>(DropboxSyncUtils.GetAutoDetectedEncodingStringFromBytes(res.data) as T));										
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
 							DropboxSyncUtils.GetAutoDetectedEncodingStringFromBytes(res.data)
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
					onResult(DropboxRequestResult<byte[]>.Error("File "+dropboxPath+" is removed on remote"));
					//onResult(new DropboxRequestResult<byte[]>(null));
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
							Log("GetFile: state of dropbox file is "+dropboxPath+" is synced now");
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
							_mainThreadQueueRunner.QueueOnMainThread(() => {
								returnCachedResult();
							});
							
							if(receiveUpdates){
								subscribeToUpdatesAction();
							}
						}else{
							if(receiveUpdates){
								// try again when internet recovers
								_internetConnectionWatcher.SubscribeToInternetConnectionRecoverOnce(() => {
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
						Log("no changes, file is deleted locally and on remote - synced");
						onSuccess();
					}									
				}
			}, onError: onError, saveChangesInfoLocally:true);									
		}
		
	}
}
