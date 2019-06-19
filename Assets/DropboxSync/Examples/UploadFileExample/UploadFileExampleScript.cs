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

public class UploadFileExampleScript : MonoBehaviour {

	public InputField localFileInput;
	public Button uploadButton;
	public Text statusText;

	void Start(){
		localFileInput.onValueChanged.AddListener((val) => {
			ValidateLocalFilePath();
		});

		ValidateLocalFilePath();

		uploadButton.onClick.AddListener(UploadFile);
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

		
		var uploadTasks = new List<Task<Metadata>>();
        for(var i = 0; i < 5; i++){
            uploadTasks.Add(DropboxSync.Main.TransferManager.UploadFileAsync(localFilePath, uploadDropboxPath, new Progress<TransferProgressReport>((report) => {
				statusText.text = $"Uploading file {report.progress}% {report.bytesPerSecondFormatted}";				
			})));
        }

        var results = await Task.WhenAll(uploadTasks);
				
        print("All file uploads completed");		

		uploadButton.interactable = true;		
	}

}
