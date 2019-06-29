// DropboxSync v2.1.1
// Created by George Fedoseev 2018-2019

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;
using UnityEngine.UI;

using System.IO;

public class FileExplorerExampleScript : MonoBehaviour {

	public Button goUpButton;

	public ScrollRect scrollRect;
	public Text fileStatusText;

	List<string> pathsHistory = new List<string>();

	// Use this for initialization
	void Start () {		
		RenderFolder("/");

		goUpButton.onClick.AddListener(() => {
			GoUp();
		});
	}

	void GoUp(){
		if(pathsHistory.Count > 1){
			pathsHistory.Remove(pathsHistory.Last());
			var prev = pathsHistory.Last();
			pathsHistory.Remove(prev);
			RenderFolder(prev);
		}
	}

	void RenderFolder(string dropboxFolderPath){
		Debug.Log("render folder "+dropboxFolderPath);
		RenderLoading();

		pathsHistory.Add(dropboxFolderPath);

		DropboxSync.Main.ListFolder(dropboxFolderPath, (folderItems) => {				
				RenderFolderItems(folderItems);			
		}, (ex) => {
			Debug.LogError($"Failed to get folder items for folder {ex}");
		});
	}

	void RenderFolderItems(List<DBXSync.Metadata> folderItems){
		// clear content
		foreach(Transform t in scrollRect.content.transform){
			Destroy(t.gameObject);
		}

		var orderedItems = folderItems.OrderBy(x => x.name).OrderByDescending(x => x.IsFolder);

		foreach(var item in orderedItems){
			var _item = item;
			if(_item.IsFolder){
				
					var go = Instantiate(Resources.Load("DBXExplorerFolderRow")) as GameObject;
					go.transform.SetParent(scrollRect.content.transform);
					go.transform.position = Vector3.zero;
					go.transform.rotation = Quaternion.identity;
					go.transform.localScale = Vector3.one;

					go.GetComponentInChildren<Text>().text = _item.name;
					go.GetComponentInChildren<Button>().onClick.AddListener(() => {
						RenderFolder(_item.path_display);						
					});

			} else {
				var _go = Instantiate(Resources.Load("DBXExplorerFileRow")) as GameObject;
				_go.transform.SetParent(scrollRect.content.transform);
				_go.transform.position = Vector3.zero;
				_go.transform.rotation = Quaternion.identity;
				_go.transform.localScale = Vector3.one;

				_go.GetComponentInChildren<Text>().text = _item.name;
				_go.GetComponentInChildren<Button>().onClick.AddListener(() => {

					DisplayFileStatus("Downloading file "+_item.path_display+"...");

					DropboxSync.Main.GetFileAsLocalCachedPath(_item.path_display, 
						new System.Progress<DBXSync.TransferProgressReport> ((progress) => {
							DisplayFileStatus($"Downloading {item.path_display}... {progress.progress}%");
						}),
						(localPath) => {
								var fileSize = new FileInfo(localPath).Length;
								DisplayFileStatus("Downloaded "+_item.path_display+" to cache.\nTotal: "+fileSize+" bytes");
						}, (ex) => {
							DisplayFileStatus($"Failed to download file: {ex}");
							Debug.LogError($"Failed to download file: {ex}");
						});
				});
			}
		}
		
	}

	void RenderLoading(){
		// clear content
		foreach(Transform t in scrollRect.content.transform){
			Destroy(t.gameObject);
		}
		
		var go = Instantiate(Resources.Load("LoadingRow")) as GameObject;
		go.transform.SetParent(scrollRect.content.transform);
		go.transform.position = Vector3.zero;
		go.transform.rotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
	}

	void DisplayFileStatus(string status){
		fileStatusText.text = status;
	}

}
