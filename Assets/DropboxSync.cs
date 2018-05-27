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

namespace DropboxSync {

	public class DropboxSync : MonoBehaviour {

		string DBXAccessToken = "2TNf3BjlBqAAAAAAAAAADBc1iIKdoEMOI2uig6oNFWtqijlveLRlDHAVDwrhbndr";

		// Use this for initialization
		void Start () {

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

			// GetFile<JsonObject>("/folder with spaces/second level depth folder/dbx_list_recursive_example.json", onResult: (result) => {
			// 	if(result.error){
			// 		Debug.LogError("Error downloading file: "+result.errorDescription);
			// 	}else{
			// 		Debug.Log("Got json cursor: "+result.data["cursor"].ToString());
			// 	}
			// }, onProgress: (progress) => {
			// 	Debug.Log(string.Format("download progress: {0}%", progress*100));
			// });

			// GetFile<JsonArray>("/folder with spaces/second level depth folder/json_array_example.json", onResult: (result) => {
			// 	if(result.error){
			// 		Debug.LogError("Error downloading file: "+result.errorDescription);
			// 	}else{
			// 		Debug.Log("Got json array: "+string.Join(", ", result.data.Select(x => x as string)));
			// 	}
			// }, onProgress: (progress) => {
			// 	Debug.Log(string.Format("download progress: {0}%", progress*100));
			// });

			// GetFileMetadata("/folder with spaces/second level depth folder/json_array_example.json", onResult: (res) => {
			// 	if(res.error){
			// 		Debug.LogError(res.errorDescription);
			// 	}else{
			// 		Debug.Log("Got metadata: "+SimpleJson.SerializeObject(res.data));
			// 	}
			// });

			CacheFile("/folder with spaces/second level depth folder/json_array_example.json", 
			onSuccess: () => {
				Debug.Log("Cached!");
			},
			onError: (errStr) => {
				Debug.LogError(errStr);
			},
			onProgress: (progress) => {

			});

			

			// GetFile<Texture2D>("/Meydanprojectsmap_scaled.jpg", onResult: (result) => {
			// 	if(result.error){
			// 		Debug.LogError("Error downloading file: "+result.errorDescription);
			// 	}else{
			// 		Debug.Log("Got Texture2D: "+result.data.width+"x"+result.data.height);
			// 		var rawImage = FindObjectOfType<RawImage>();
			// 		rawImage.texture = result.data;
			// 		rawImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)result.data.width/result.data.height;
			// 	}
			// }, onProgress: (progress) => {
			// 	Debug.Log(string.Format("download progress: {0}%", progress*100));
			// });
		}
		
		// Update is called once per frame
		void Update () {
			
		}

		// METHODS

		public void GetFile<T>(string path, Action<DropboxFileDownloadRequestResult<T>> onResult, Action<float> onProgress = null) where T : class{
			Action<DropboxFileDownloadRequestResult<byte[]>> onResultMiddle = null;

			if(typeof(T) == typeof(string)){
				// TEXT DATA
				onResultMiddle = (res) => {					
					onResult(new DropboxFileDownloadRequestResult<T>(DropboxSyncUtils.GetAudtoDetectedEncodingStringFromBytes(res.data) as T, res.fileMetadata));										
				};				
			}
			else if(typeof(T) == typeof(JsonObject) || typeof(T) == typeof(JsonArray)){
				// JSON OBJECT/ARRAY
				onResultMiddle = (res) => {					
					onResult(new DropboxFileDownloadRequestResult<T>(SimpleJson.DeserializeObject(
						DropboxSyncUtils.GetAudtoDetectedEncodingStringFromBytes(res.data)
					) as T, res.fileMetadata));
				};	
			}
			else if(typeof(T) == typeof(Texture2D)){
				// IMAGE DATA
				onResultMiddle = (res) => {					
					onResult(new DropboxFileDownloadRequestResult<T>(DropboxSyncUtils.LoadImageToTexture2D(res.data) as T, res.fileMetadata));
				};	
			}
			else{
				onResult(DropboxFileDownloadRequestResult<T>.Error(string.Format("Dont have a mapping byte[] -> {0}. Type {0} is not supported.", typeof(T).ToString())));
				return;
			}

			GetFile(path, onResultMiddle, onProgress);
		}

		public void GetFile(string path, Action<DropboxFileDownloadRequestResult<byte[]>> onResult, Action<float> onProgress = null){
			var prms = new DropboxDownloadFileRequestParams(path);
			MakeDropboxDownloadRequest("https://content.dropboxapi.com/2/files/download", prms,
			onResponse: (fileMetadata, data) => {
				onResult(new DropboxFileDownloadRequestResult<byte[]>(data, fileMetadata));
			},
			onProgress: onProgress,
			onWebError: (webErrorStr) => {
				onResult(DropboxFileDownloadRequestResult<byte[]>.Error(webErrorStr));
			});
		}

		public void GetFileMetadata(string path, Action<DropboxRequestResult<JsonObject>> onResult){
			var prms = new DropboxGetMetadataRequestParams(path);

			//Log("GetFileMetadata");
			MakeDropboxRequest("https://api.dropboxapi.com/2/files/get_metadata", prms, 
			onResponse: (jsonStr) => {
				onResult(new DropboxRequestResult<JsonObject>(SimpleJson.DeserializeObject(jsonStr) as JsonObject));
			},
			onProgress:null,
			onWebError: (errStr) => {
				//Log("GetFileMetadata:onWebError");
				onResult(DropboxRequestResult<JsonObject>.Error(errStr));
			});
		}

		void CacheFile(string dropboxPath, Action onSuccess, Action<float> onProgress, Action<string> onError){

			var metadataFilePath = GetMetadataFilePath(dropboxPath);

			JsonObject remoteMedatadata = null;

			if(File.Exists(metadataFilePath)){
				// get local content hash
				var fileJsonStr = File.ReadAllText(metadataFilePath);
				string local_content_hash = null;
				bool localMetadataReadError = false;
				try {
					var localJsonMetadata = SimpleJson.DeserializeObject(fileJsonStr) as JsonObject;
					local_content_hash = localJsonMetadata["content_hash"].ToString();
				}catch{
					localMetadataReadError = true;					
				}				

				if(!localMetadataReadError){
					// request for metadata to get remote content hash
					//Log("Getting metadata");
					GetFileMetadata(dropboxPath, onResult: (res) => {
						if(res.error){							
							if (res.errorDescription.Contains("not_found")){
								//Log("file not found");
								// delete file from cache
								DeleteFileFromCache(dropboxPath);
							}else{
								onError(res.errorDescription);
							}
							return;						
						}else{
							//Log("Got metadata");
							remoteMedatadata = res.data;
							var remote_content_hash = remoteMedatadata["content_hash"].ToString();

							if(local_content_hash != remote_content_hash){
								DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
							}
						}
					});
				}else{
					//Log("Getting local metadata error - download to cache");
					DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);	
				}				
				
			}else{
				DownloadToCache(dropboxPath, onSuccess: onSuccess, onProgress: onProgress, onError: onError);
			}

			
		}

		void DeleteFileFromCache(string dropboxPath){
			var localFilePath = GetFilePathInCache(dropboxPath);
			if(File.Exists(localFilePath)){
				File.Delete(localFilePath);
			}		

			var metadataFilePath = GetMetadataFilePath(dropboxPath);
			if(File.Exists(metadataFilePath)){
				File.Delete(metadataFilePath);
			}
		}

		void DownloadToCache (string dropboxPath, Action onSuccess, Action<float> onProgress, Action<string> onError){
				//Log("DownloadToCache");
				GetFile(dropboxPath, (res) => {
					if(res.error){
						onError(res.errorDescription);						
					}else{
						var localFilePath = GetFilePathInCache(dropboxPath);
						Log("Cache folder path: "+CacheFolderPathForToken	);
						Log("Local cached file path: "+localFilePath);					
						

						// make sure containing directory exists
						var fileDirectoryPath = Path.GetDirectoryName(localFilePath);
						Log("Local cached directory path: "+fileDirectoryPath);
						Directory.CreateDirectory(fileDirectoryPath);

						File.WriteAllBytes(localFilePath, res.data);						

						// write metadata to separate file near
						var newMetadataFilePath = GetMetadataFilePath(dropboxPath);
						File.WriteAllText(newMetadataFilePath, SimpleJson.SerializeObject(res.fileMetadata));

						Log("Wrote metadata file "+newMetadataFilePath);

						onSuccess();
					}
				}, onProgress: onProgress);
			}

		string GetFilePathInCache(string dropboxPath){
			var relativeDropboxPath = dropboxPath.Substring(1);
			return Path.Combine(CacheFolderPathForToken, relativeDropboxPath);
		}

		string GetMetadataFilePath(string dropboxPath){
			return GetFilePathInCache(dropboxPath)+".dbxsync";
		}

		string CacheFolderPathForToken {
			get {
				var accessTokeFirst5Characters = DBXAccessToken.Substring(0, 5);
				return Path.Combine(Application.persistentDataPath, accessTokeFirst5Characters);
			}		
		}

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

				JsonObject root = null;
				try {
					root = SimpleJson.DeserializeObject(jsonStr) as JsonObject;
				}catch(Exception ex){
					onError(ex.Message);
					return;
				}

				var entries = root["entries"] as JsonArray;
				foreach(JsonObject entry in entries){
					if(entry[".tag"].ToString() == "file"){
						currentResults.Add(DBXFile.FromDropboxJsonObject(entry));
					}else if(entry[".tag"].ToString() == "folder"){
						currentResults.Add(DBXFolder.FromDropboxJsonObject(entry));
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
								onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
							}else{
								// return progress is going but unknown
								onProgress(-1);
							}
						}						
					};

					

					client.UploadDataCompleted += (s, e) => {
						//Log("MakeDropboxRequest:client.UploadDataCompleted");
						if(e.Error != null){
							//Log("MakeDropboxRequest:client.UploadDataCompleted:Error");
							//var respStr = Encoding.UTF8.GetString(e.Result);
							var webex = e.Error as WebException;
							var stream = webex.Response.GetResponseStream();
							var reader = new StreamReader(stream);
							var responseStr = reader.ReadToEnd();
							var json = SimpleJson.DeserializeObject(responseStr) as JsonObject;
							var errorSummary = json["error_summary"].ToString();
							//Log(responseStr);
							onWebError(errorSummary);
						}else{
							var respStr = Encoding.UTF8.GetString(e.Result);
							onResponse(respStr);
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

		void MakeDropboxDownloadRequest<T>(string url, T parametersObject, Action<JsonObject, byte[]> onResponse, Action<float> onProgress, Action<string> onWebError) where T : DropboxRequestParams{
			MakeDropboxDownloadRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}

		void MakeDropboxDownloadRequest(string url, string jsonParameters, Action<JsonObject, byte[]> onResponse, Action<float> onProgress, Action<string> onWebError){
			try {
				using (var client = new WebClient()){				
					client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);					
					client.Headers.Set("Dropbox-API-Arg", jsonParameters);
					
					client.DownloadProgressChanged += (s, e) => {
						
						if(onProgress != null){
							//Log(string.Format("Downloaded {0} bytes out of {1} ({2}%)", e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage));
							if(e.TotalBytesToReceive != -1){
								// if download size in known from server
								onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
							}else{
								// return progress is going but unknown
								onProgress(-1);
							}
						}						
					};

					client.DownloadDataCompleted += (s, e) => {
						if(e.Error != null){
							var webex = e.Error as WebException;
							var stream = webex.Response.GetResponseStream();
							var reader = new StreamReader(stream);
							var responseStr = reader.ReadToEnd();
							var json = SimpleJson.DeserializeObject(responseStr) as JsonObject;
							var errorSummary = json["error_summary"].ToString();
							//Log(responseStr);
							onWebError(errorSummary);
						}else if(e.Cancelled){
							onWebError("Download was cancelled.");
						}else{
							//var respStr = Encoding.UTF8.GetString(e.Result);
							var metadataJsonStr = client.ResponseHeaders["Dropbox-API-Result"].ToString();
							Log(metadataJsonStr);
							var fileMetadata = SimpleJson.DeserializeObject(metadataJsonStr) as JsonObject;
							onResponse(fileMetadata, e.Result);
						}
					};

					var uri = new Uri(url);
					client.DownloadDataAsync(uri);
				}
			} catch (WebException ex){
				onWebError(ex.Message);
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

		void TestGetMetadata(){
			using (var client = new WebClient()){
				var url = "https://api.dropboxapi.com/2/files/get_metadata";
				client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
				client.Headers.Set("Content-Type", "application/json");				

				var par = new DropboxGetMetadataRequestParams("/folder with spaces");
			

				var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
				var respStr = Encoding.UTF8.GetString(respBytes);
				
				Log(respStr);
			}
		}

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