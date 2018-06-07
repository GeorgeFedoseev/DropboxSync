using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;

public class DropboxSyncFoldersExampleScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
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

	}

}
