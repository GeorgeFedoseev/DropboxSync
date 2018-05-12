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
	public bool recursive = false;
	public bool include_media_info = false;
	public bool include_deleted = false;
	public bool include_has_explicit_shared_members = false;
	public bool include_mounted_folders = false;

}

public class DropboxSync : MonoBehaviour {

	string accessToken = "sVawC-LSz14AAAAAAAAILN_nDwbybSkfaYkjXqJcoapoMI9NrFvh9iZoGb8jtkBq";

	// Use this for initialization
	void Start () {
		TestDropbox();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	// METHODS

	void TestDropbox(){
		
		InitiateSSLTrust();

		using (var client = new WebClient()){
			
			var url = "https://api.dropboxapi.com/2/files/list_folder";
			client.Headers.Set("Authorization", "Bearer "+accessToken);
			client.Headers.Set("Content-Type", "application/json");				

			var par = new DropboxListFolderParams{path=""};

			// var postData =	new System.Collections.Specialized.NameValueCollection(){
			// 	{ "path", "/" }				
			// };

			var respBytes = client.UploadData(url, "POST", Encoding.Default.GetBytes(JsonUtility.ToJson(par)));
			var respStr = Encoding.UTF8.GetString(respBytes);
			Debug.Log(respStr);
				
			
		}
	}

	public static void InitiateSSLTrust()
	{
		try
		{
			//Change SSL checks so that all checks pass
			ServicePointManager.ServerCertificateValidationCallback =
			new RemoteCertificateValidationCallback(
					delegate
					{ return true; }
				);
		}
		catch (Exception ex)
		{
			Debug.LogException(ex);
		}
	}

	// EVENTS
}
