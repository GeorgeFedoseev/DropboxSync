/// DropboxSync v2.1.1
// Created by George Fedoseev 2018-2019

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Linq;
using System.Text;
using System.Threading;

public class UploadTextExampleScript : MonoBehaviour {

	string TEXT_FILE_UPLOAD_PATH = "/DropboxSyncExampleFolder/uploaded_text.txt";

	public Text inputLabelText, outputLabelText;

	public InputField textToUploadInput;
	public Text downloadedText;
	public Button uploadTextButton;
	public Button cancelUploadButton;

	private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

	// Use this for initialization
	void Start () {
		inputLabelText.text = string.Format("Enter text to upload to <b>{0}</b>:", TEXT_FILE_UPLOAD_PATH);
		outputLabelText.text = string.Format("Remote Dropbox file: <b>{0}</b> contents (updated from Dropbox):", TEXT_FILE_UPLOAD_PATH);

		// subscribe to remote file changes
		DropboxSync.Main.GetFile<string>(TEXT_FILE_UPLOAD_PATH, new Progress<TransferProgressReport>((progress) => {}), (str) => {				
			Debug.Log($"Received text \"{str}\" from Dropbox!");			
			UpdateDownloadedText(str);			
		}, (ex) => {
			Debug.LogError($"Error getting text string: {ex}");
			UpdateDownloadedText($"Error getting text string: {ex}");
		}, receiveUpdates:true);

		
		uploadTextButton.onClick.AddListener(UploadTextButtonClicked);
		cancelUploadButton.onClick.AddListener(() => {
			_cancellationTokenSource.Cancel();
		});
	}


	public void UploadTextButtonClicked(){		
		_cancellationTokenSource = new CancellationTokenSource();
		
		textToUploadInput.interactable = false;
		uploadTextButton.interactable = false;

		Debug.Log("Upload text "+textToUploadInput.text);

		DropboxSync.Main.UploadFile(Encoding.UTF8.GetBytes(textToUploadInput.text), TEXT_FILE_UPLOAD_PATH,
									new Progress<TransferProgressReport>((progress) => {}),
		(metadata) => {			
			Debug.Log("Upload completed");
			textToUploadInput.text = "";
			textToUploadInput.interactable = true;
			uploadTextButton.interactable = true;			
		}, (ex) => {
			Debug.LogError("Error uploading text file: "+ex.Message);
			textToUploadInput.interactable = true;
			uploadTextButton.interactable = true;
		}, _cancellationTokenSource.Token);
	}
	
	void UpdateDownloadedText(string desc){
		downloadedText.text = desc;
	}

}
