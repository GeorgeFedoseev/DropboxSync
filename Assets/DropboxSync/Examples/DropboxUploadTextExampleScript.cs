using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Linq;
using System.Text;

public class DropboxUploadTextExampleScript : MonoBehaviour {

	string TEXT_FILE_PATH = "/DropboxSyncExampleFolder/new_dir/uploaded_text.txt";

	public InputField textToUploadInput;
	public Text downloadedText;
	public Button uploadTextButton;

	// Use this for initialization
	void Start () {

		// <TESTING

		// create folder
		// DropboxSync.Main.CreateFolder("/DropboxSyncExampleFolder/text_files", (res) => {
		// 	if(res.error != null){
		// 		Debug.LogError("Failed to create folder: "+res.error.ErrorDescription + " - "+res.error.ErrorType.ToString());
		// 	}else{
		// 		Debug.LogWarning("Folder created");
		// 	}
		// });


		// get parent folders from path
		// var p = "/adsada/dsfdsf/eeee.jpg/bbbb/mmmmmm";
		// var path_folders = DBXSync.Utils.DropboxSyncUtils.GetPathFolders(p);
		// path_folders.ForEach(x => {Debug.Log(x);});

		// check if path exists
		// DropboxSync.Main.PathExists("/DropboxSyncExampleFolder/uploaded_text.txt", (res) => {
		// 	if(res.error != null){
		// 		Debug.LogError("Failed to check if path exists: "+res.error.ErrorDescription);
		// 	}else{
		// 		Debug.LogWarning("Path exists: "+res.data.ToString());
		// 	}
		// });

	


		// TESTING>


		// subscribe to remote file changes
		DropboxSync.Main.GetFile<string>(TEXT_FILE_PATH, (res) => {
			if(res.error != null){
				Debug.LogError("Error getting text string: "+res.error.ErrorDescription);
			}else{
				Debug.Log("Received text string from Dropbox!");
				var textStr = res.data;
				UpdateDownloadedText(textStr);
			}
		}, receiveUpdates:true);

		// subscribe to upload button click
		uploadTextButton.onClick.AddListener(UploadTextButtonClicked);		
	}


	public void UploadTextButtonClicked(){
		Debug.Log("Upload text "+textToUploadInput.text);
		DropboxSync.Main.UploadFile(TEXT_FILE_PATH, Encoding.UTF8.GetBytes(textToUploadInput.text), (res) => {
			if(res.error != null){
				Debug.LogError("Error uploading text file: "+res.error.ErrorDescription);
			}else{
				Debug.Log("Upload completed");
			}			
		}, (progress) => {
			Debug.Log("Upload progress: "+progress.ToString());
		});
	}
	
	void UpdateDownloadedText(string desc){
		downloadedText.text = desc;
	}

}
