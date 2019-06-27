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
	public Button cancelButton;
	public Text statusText;

	private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

	private string _uploadDropboxPath;

	void Start(){
		localFileInput.onValueChanged.AddListener((val) => {
			ValidateLocalFilePath();
		});

		ValidateLocalFilePath();

		uploadButton.onClick.AddListener(UploadFile);	

		cancelButton.onClick.AddListener(() => {
			_cancellationTokenSource.Cancel();
		});
	}	

	void ValidateLocalFilePath(){
		if(File.Exists(localFileInput.text)){
			_uploadDropboxPath = Path.Combine("/DropboxSyncExampleFolder/", Path.GetFileName(localFileInput.text));
			statusText.text = $"Ready to upload to {_uploadDropboxPath}";
			uploadButton.interactable = true;
		}else{
			statusText.text = "<color=red>Specified file does not exist.</color>";
			uploadButton.interactable = false;
		}
	}

	void UploadFile(){
		_cancellationTokenSource  = new CancellationTokenSource();
		uploadButton.interactable = false;
		var localFilePath = localFileInput.text;
		

		DropboxSync.Main.UploadFile(localFilePath, _uploadDropboxPath, new Progress<TransferProgressReport>((report) => {
				if(Application.isPlaying){					
					statusText.text = $"Uploading file {report.progress}% {report.bytesPerSecondFormatted}";
				}				
		}), (metadata) => {
			// success			
			print($"Upload completed:\n{metadata}");
			statusText.text = $"<color=green>Uploaded. {metadata.id}</color>";

			uploadButton.interactable = true;		
		}, (ex) => {
			// exception
			if(ex is OperationCanceledException){
				Debug.Log("Upload cancelled");
				statusText.text = $"<color=orange>Upload canceled.</color>";
			}else{
				Debug.LogException(ex);
				statusText.text = $"<color=red>Upload failed.</color>";
			}

			uploadButton.interactable = true;		
		}, _cancellationTokenSource.Token);
		
	}

}
