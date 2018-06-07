using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



public class DropboxSyncExampleScript : MonoBehaviour {

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
			// 		Debug.Log("Got json array: "+string.Join(", ", result.data.Select(x => x as string).ToArray()));					
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


			
			
			// DropboxSync.Main.FolderGetRemoteChanges("/HelloThereFolder", onResult: (res) => {
			// 	if(res.error){
			// 		Debug.LogError(res.errorDescription);
			// 	}else{
			// 		Debug.Log("File changes: "+res.data.Count);
			// 		foreach(var change in res.data){
			// 			Debug.Log(string.Format("{0} - {1}", change.file.path, change.change.ToString()));
			// 		}
			// 	}
			// });

			DropboxSync.Main.SyncFolderFromDropbox("/helloThereFolder", () => {
				Debug.Log("Synced folder!");
			},
			(progress) => {
				Debug.Log(string.Format("Syncing folder: {0}%", progress*100));
			},
			(errorStr) => {
				Debug.LogError(errorStr);
			});
			
			// SUBSCRIBE TO FOLDER CHANGES
			DropboxSync.Main.SubscribeToFolderChanges("/helloThereFolder", (changes) => {
					Debug.Log("File changes: "+changes.Count);
					foreach(var change in changes){
						Debug.Log(string.Format("{0} - {1}", change.file.path, change.changeType.ToString()));
					}
			});



			// GET PICTURE AND ITS UPDATES
			Action<Texture2D> updatePic = (tex) => {
				var rawImage = FindObjectOfType<RawImage>();
				rawImage.texture = tex;
				rawImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)tex.width/tex.height;
			};

			var imageDropbobxPath = "/Meydanprojectsmap_scaled.jpg";			

			
			DropboxSync.Main.GetFile<Texture2D>(imageDropbobxPath, (res) => {
				if(res.error){
					Debug.LogError(res.errorDescription);
				}else{
					Debug.Log("received texture");
					updatePic(res.data);
				}
			}, useCachedIfPossible:false, useCachedIfOffline:true, receiveUpdates:true);
		
	}

}
