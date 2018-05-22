using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

[Serializable]
public class DropboxListFolderParams {
	public string path;
	public bool recursive = true;
	public bool include_media_info = false;
	public bool include_deleted = false;
	public bool include_has_explicit_shared_members = false;
	public bool include_mounted_folders = false;
}

public class DropboxGetMetadataParams {
	public string path;	
	public bool include_media_info = false;
	public bool include_deleted = false;
	public bool include_has_explicit_shared_members = false;	
}



public class DropboxSync : MonoBehaviour {

	string accessToken = "2TNf3BjlBqAAAAAAAAAADBc1iIKdoEMOI2uig6oNFWtqijlveLRlDHAVDwrhbndr";

	// Use this for initialization
	void Start () {
		//TestListFolder();

		TestGetMetadata();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	// METHODS

	void TestListFolder(){
		using (var client = new WebClient()){
			
			var url = "https://api.dropboxapi.com/2/files/list_folder";
			client.Headers.Set("Authorization", "Bearer "+accessToken);
			client.Headers.Set("Content-Type", "application/json");				

			var par = new DropboxListFolderParams{path=""};
		

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
			client.Headers.Set("Authorization", "Bearer "+accessToken);
			client.Headers.Set("Content-Type", "application/json");				

			var par = new DropboxGetMetadataParams{path="/folder with spaces"};
		

			var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
			var respStr = Encoding.UTF8.GetString(respBytes);
			
			Debug.Log(respStr);
		}
	}



	// EVENTS
}
