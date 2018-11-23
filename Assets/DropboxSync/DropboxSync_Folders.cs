// DropboxSync v1.1
// Created by George Fedoseev 2018

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

using DBXSync.Model;
using DBXSync.Utils;
using UnityEngine.UI;
using System.IO;
using System.Threading;

namespace DBXSync {
	public partial class DropboxSync: MonoBehaviour {

		private static readonly string LIST_FOLDER_ENDPOINT = "https://api.dropboxapi.com/2/files/list_folder";
		private static readonly string LIST_FOLDER_CONTINUE_ENDPOINT = "https://api.dropboxapi.com/2/files/list_folder/continue";
		private static readonly string CREATE_FOLDER_ENDPOINT = "https://api.dropboxapi.com/2/files/create_folder_v2";

		// FOLDERS
		
		
		public void PathExists(string dropboxFolderPath, Action<DropboxRequestResult<bool>> onResult){
			GetMetadata<DBXFolder>(dropboxFolderPath, (res) => {
				if(res.error != null){
					if(res.error.ErrorType == DBXErrorType.RemotePathNotFound){
						// path not found
						_mainThreadQueueRunner.QueueOnMainThread(() => {
							onResult(new DropboxRequestResult<bool>(false));
						});
					}else{
						// some other error
						_mainThreadQueueRunner.QueueOnMainThread(() => {
							onResult(DropboxRequestResult<bool>.Error(res.error));
						});
					}					
				}else{
					// path exists
					_mainThreadQueueRunner.QueueOnMainThread(() => {
						onResult(new DropboxRequestResult<bool>(true));
					});					
				}
			});

		}

		public void CreateFolder(string dropboxFolderPath, Action<DropboxRequestResult<DBXFolder>> onResult) {
			var path = DropboxSyncUtils.NormalizePath(dropboxFolderPath);

			var prms = new DropboxCreateFolderRequestParams();
			prms.path = path;

			MakeDropboxRequest(CREATE_FOLDER_ENDPOINT, prms, (jsonStr) => {

				DBXFolder folderMetadata = null;

				try {
					var root = JSON.FromJson<Dictionary<string, object>>(jsonStr);
					folderMetadata = DBXFolder.FromDropboxDictionary(root["metadata"] as Dictionary<string, object>);
				}catch(Exception ex){
					_mainThreadQueueRunner.QueueOnMainThread(() => {
						onResult(DropboxRequestResult<DBXFolder>.Error(new DBXError(ex.Message, DBXErrorType.ParsingError)));
					});
					return;
				}							
				
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(new DropboxRequestResult<DBXFolder>(folderMetadata));
				});				
			}, onProgress: (progress) => {}, (error) => {
				if(error.ErrorDescription.Contains("path/conflict/folder")){
					error.ErrorType = DBXErrorType.RemotePathAlreadyExists;
				}
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(DropboxRequestResult<DBXFolder>.Error(error));
				});
			});
		}

		public void GetFolderStructure(string dropboxFolderPath, Action<DropboxRequestResult<DBXFolder>> onResult,
						 Action<float> onProgress = null){
			var path = DropboxSyncUtils.NormalizePath(dropboxFolderPath);

			_GetFolderItemsFlat(path, onResult: (items) => {
				DBXFolder rootFolder = null;

				// get root folder
				if(path == "/"){
					rootFolder = new DBXFolder{id="", path="/", name="", items = new List<DBXItem>()};			
				}else{
					rootFolder = items.Where(x => x.path == path).First() as DBXFolder;			
				}
				// squash flat results
				rootFolder = BuildStructureFromFlat(rootFolder, items);

				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(new DropboxRequestResult<DBXFolder>(rootFolder));
				});
			},
			onProgress: (progress) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onProgress(progress);
				});
			},
			onError: (errorStr) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(DropboxRequestResult<DBXFolder>.Error(errorStr));
				});
			}, recursive: true);
		}

		public void GetFolderItems(string path, Action<DropboxRequestResult<List<DBXItem>>> onResult, Action<float> onProgress = null, bool recursive = false){
			_GetFolderItemsFlat(path, onResult: (items) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(new DropboxRequestResult<List<DBXItem>>(items));	
				});				
			},
			onProgress: (progress) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onProgress(progress);
				});
			},
			onError: (errorStr) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(DropboxRequestResult<List<DBXItem>>.Error(errorStr));
				});
			}, recursive: recursive);
		}

		DBXFolder BuildStructureFromFlat(DBXFolder rootFolder, List<DBXItem> pool){		
			foreach(var poolItem in pool){
				// if item is immediate child of rootFolder
				if(DropboxSyncUtils.IsPathImmediateChildOfFolder(rootFolder.path, poolItem.path)){
					// add poolItem to folder children
					if(poolItem.type == DBXItemType.Folder){
						//Debug.Log("Build structure recursive");
						rootFolder.items.Add(BuildStructureFromFlat(poolItem as DBXFolder, pool));	
					}else{
						rootFolder.items.Add(poolItem);	
					}				
					//Debug.Log("Added child "+poolItem.path);			
				}
			}

			return rootFolder;
		}

		void _GetFolderItemsFlat(string folderPath, Action<List<DBXItem>> onResult, Action<float> onProgress,
				 Action<DBXError> onError, bool recursive = false, string requestCursor = null, List<DBXItem> currentResults = null){
			folderPath = DropboxSyncUtils.NormalizePath(folderPath);

			if(folderPath == "/"){
				folderPath = ""; // dropbox error fix
			}

			string url;
			DropboxRequestParams prms;
			if(requestCursor == null){
				// first request
				currentResults = new List<DBXItem>();
				url = LIST_FOLDER_ENDPOINT;
				prms = new DropboxListFolderRequestParams{path=folderPath, recursive=recursive};
			}else{
				// have cursor to continue list
				url = LIST_FOLDER_CONTINUE_ENDPOINT;
				prms = new DropboxContinueWithCursorRequestParams(requestCursor);
			}
			
			MakeDropboxRequest(url, prms, onResponse: (jsonStr) => {
				//Log("Got reponse: "+jsonStr);

				Dictionary<string, object> root = null;
				try {
					root = JSON.FromJson<Dictionary<string, object>>(jsonStr);
				}catch(Exception ex){
					onError(new DBXError(ex.Message, DBXErrorType.ParsingError));
					return;
				}

				var entries = root["entries"] as List<object>;
				foreach(Dictionary<string, object> entry in entries){
					if(entry[".tag"].ToString() == "file"){
						currentResults.Add(DBXFile.FromDropboxDictionary(entry));
					}else if(entry[".tag"].ToString() == "folder"){
						currentResults.Add(DBXFolder.FromDropboxDictionary(entry));
					}else{
						onError(new DBXError("Unknown entry tag "+entry[".tag".ToString()], DBXErrorType.Unknown));
						return;
					}
				}

				if((bool)root["has_more"]){
					// recursion
					_GetFolderItemsFlat(folderPath, onResult, onProgress, onError, recursive: recursive,
					requestCursor:root["cursor"].ToString(), 
					currentResults: currentResults);
				}else{
					// done
					onResult(currentResults);
				}

			}, onProgress: onProgress,
				onWebError: (webErrorStr) => {
				//LogError("Got web err: "+webErrorStr);
				onError(webErrorStr);
			});
		}
		
	}
}
