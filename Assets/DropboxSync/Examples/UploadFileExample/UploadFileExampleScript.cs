/// DropboxSync v2.1.1
// Created by George Fedoseev 2018-2019

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

public class UploadFileExampleScript : MonoBehaviour {

	public InputField localFileInput;
	public Button uploadButton;
	public Button cancelDuplicateButton;
	public Text statusText;

	private CancellationTokenSource _duplicateCancellationTokenSource;

	void Start(){
		localFileInput.onValueChanged.AddListener((val) => {
			ValidateLocalFilePath();
		});

		ValidateLocalFilePath();

		uploadButton.onClick.AddListener(UploadFile);	

		_duplicateCancellationTokenSource = new CancellationTokenSource();
		cancelDuplicateButton.onClick.AddListener(() => {
			_duplicateCancellationTokenSource.Cancel();
		});
	}	

	void ValidateLocalFilePath(){
		if(File.Exists(localFileInput.text)){
			statusText.text = "Ready to upload.";
			uploadButton.interactable = true;
		}else{
			statusText.text = "<color=red>Specified file does not exist.</color>";
			uploadButton.interactable = false;
		}
	}

	async void UploadFile(){
		uploadButton.interactable = false;
		var localFilePath = localFileInput.text;
		var uploadDropboxPath = Path.Combine("/DropboxSyncExampleFolder/", Path.GetFileName(localFilePath));

		var originalUploadTask = DropboxSync.Main.TransferManager.UploadFileAsync(localFilePath, uploadDropboxPath, new Progress<TransferProgressReport>((report) => {
				if(Application.isPlaying){
					statusText.text = $"Uploading file {report.progress}% {report.bytesPerSecondFormatted}";
				}				
		}));

		var duplicateUploadTask = DropboxSync.Main.TransferManager.UploadFileAsync(localFilePath, uploadDropboxPath, new Progress<TransferProgressReport>((report) => {
				if(Application.isPlaying){
					Debug.Log($"Duplicate uploading file {report.progress}% {report.bytesPerSecondFormatted}");
				}				
		}), _duplicateCancellationTokenSource.Token);
		
		
		
		var results = await Task.WhenAll(originalUploadTask, duplicateUploadTask);
		print("All file uploads completed");        

		uploadButton.interactable = true;		
	}

}
