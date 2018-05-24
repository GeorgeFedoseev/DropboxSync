using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

[Serializable]
public class DropboxRequestParams {

}

[Serializable]
public class DropboxListFolderParams : DropboxRequestParams {
	public string path;
	public bool recursive = true;
	public bool include_media_info = false;
	public bool include_deleted = false;
	public bool include_has_explicit_shared_members = false;
	public bool include_mounted_folders = false;
}

public class DropboxCursorParams : DropboxRequestParams {
	public string cursor;

	public DropboxCursorParams(string cur) {
		cursor = cur;
	}
}


[Serializable]
public class DropboxGetMetadataParams : DropboxRequestParams {
	public string path;	
	public bool include_media_info = false;
	public bool include_deleted = false;
	public bool include_has_explicit_shared_members = false;	
}

public class DropboxRequestResult<T> {
	public T res;
	public bool error = false;
	public string errorDescription = null;

	public DropboxRequestResult(T res){
        this.res = res;
    }

	public static DropboxRequestResult<T> Error(string errorDescription){
		var inst = new DropboxRequestResult<T>(default(T));
		inst.error = true;
		inst.errorDescription = errorDescription;
		return inst;
	}
}



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
		
		GetFolder("/folder with spaces", onResult: (res) => {
			if(res.error){
				Debug.LogError(res.errorDescription);
			}
		});

		//TestGetMetadata();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	// METHODS

	static string NormalizeFolderPath(string strPath){		
		var components = strPath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
		return ("/"+string.Join("/", components)).ToLower();
	}

	void GetFolder(string path, Action<DropboxRequestResult<DBXFolder>> onResult){
		path = NormalizeFolderPath(path);

		GetFolderItemsFlatRecursive(path, onResult: (items) => {
			var rootFolder = items.Where(x => x.path == path).First();			
			Debug.Log("Got root folder "+rootFolder.path);

			// squash flat results
			



		}, onError: (errorStr) => {
			onResult(DropboxRequestResult<DBXFolder>.Error(errorStr));
		});
	}

	void GetFolderItemsFlatRecursive(string folderPath, Action<List<DBXItem>> onResult, Action<string> onError, string requestCursor = null, List<DBXItem> currentResults = null){

		string url;
		DropboxRequestParams prms;
		if(requestCursor == null){
			// first request
			currentResults = new List<DBXItem>();
			url = "https://api.dropboxapi.com/2/files/list_folder";
			prms = new DropboxListFolderParams{path=folderPath, recursive=true};
		}else{
			// have cursor to continue list
			url = "https://api.dropboxapi.com/2/files/list_folder/continue";
			prms = new DropboxCursorParams(requestCursor);
		}

		
		MakeDropboxRequest(url, prms, onResponse: (jsonStr) => {
			Debug.Log("Got reponse: "+jsonStr);

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

			Debug.Log("Current results: "+currentResults.Count);

			if((bool)root["has_more"]){
				// recursion
				GetFolderItemsFlatRecursive(folderPath, onResult, onError, root["cursor"].ToString(), currentResults);
			}else{
				// done
				onResult(currentResults);
			}
			

		}, onWebError: (webErrorStr) => {
			//Debug.LogError("Got web err: "+webErrorStr);
			onError(webErrorStr);
		});
	}

	void MakeDropboxRequest<T>(string url, T parametersObject, Action<string> onResponse, Action<string> onWebError){
		MakeDropboxRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onWebError);
	}

	void MakeDropboxRequest(string url, string jsonParameters, Action<string> onResponse, Action<string> onWebError){
		try {
			using (var client = new WebClient()){				
				client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
				client.Headers.Set("Content-Type", "application/json");
				var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(jsonParameters));
				var respStr = Encoding.UTF8.GetString(respBytes);	
				onResponse(respStr);			
			}
		} catch (WebException ex){
			onWebError(ex.Message);
		}
	}

	void TestListFolder(){
		using (var client = new WebClient()){
			
			var url = "https://api.dropboxapi.com/2/files/list_folder";
			client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
			client.Headers.Set("Content-Type", "application/json");				

			var par = new DropboxListFolderParams{path="/asdsa"};
		

			var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
			var respStr = Encoding.UTF8.GetString(respBytes);
			
			Debug.Log(respStr);

			var root = SimpleJson.DeserializeObject(respStr) as JsonObject;
			var entries = root["entries"] as JsonArray;

			var item = DBXFolder.FromDropboxJsonObject(entries[0] as JsonObject);

			Debug.Log(JsonUtility.ToJson(item, prettyPrint:true));
			
		}
	}

	void TestGetMetadata(){
		using (var client = new WebClient()){
			var url = "https://api.dropboxapi.com/2/files/get_metadata";
			client.Headers.Set("Authorization", "Bearer "+DBXAccessToken);
			client.Headers.Set("Content-Type", "application/json");				

			var par = new DropboxGetMetadataParams{path="/folder with spaces"};
		

			var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
			var respStr = Encoding.UTF8.GetString(respBytes);
			
			Debug.Log(respStr);
		}
	}



	// EVENTS
}
