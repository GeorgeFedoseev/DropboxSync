using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Linq;
using System.Text;

public class DropboxSyncUploadFileExampleScript : MonoBehaviour {

	string TEXT_FILE_PATH = "/DropboxSyncExampleFolder/uploaded_text.txt";

	public InputField textToUploadInput;
	public Text downloadedText;
	public Button uploadTextButton;

	// Use this for initialization
	void Start () {

		// subscribe to remote file
		DropboxSync.Main.GetFile<string>(TEXT_FILE_PATH, (res) => {
			if(res.error){
				Debug.LogError("Error getting text string: "+res.errorDescription);
			}else{
				Debug.Log("Received text string from Dropbox!");
				var textStr = res.data;
				UpdateDownloadedText(textStr);
			}
		}, receiveUpdates:true);

		// textToUploadInput.onValueChange.AddListener((val) => {
		// 	Debug.Log("Text changed to "+val);
		// });

		uploadTextButton.onClick.AddListener(UploadTextButtonClicked);		
	}


	public void UploadTextButtonClicked(){
		Debug.Log("Upload text "+textToUploadInput.text);
		DropboxSync.Main.UploadFile(TEXT_FILE_PATH, Encoding.UTF8.GetBytes(textToUploadInput.text), (res) => {
			if(res.error){
				Debug.LogError("Error uploading text file: "+res.errorDescription);
			}else{
				Debug.Log("Upload completed");
			}			
		}, (progress) => {
			Debug.Log("Upload progress: "+progress.ToString());
		});
	}
	// UI-update methods

	void UpdateDownloadedText(string desc){
		downloadedText.text = desc;
	}

}
