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

		// SUBSCRIBING TO CHANGES

		Dictionary<DBXItem, List<Action<List<DBXFileChange>>>> OnChangeCallbacksDict = new Dictionary<DBXItem, List<Action<List<DBXFileChange>>>>();
		void CheckChangesForSubscribedItems(){
			if(OnChangeCallbacksDict.Count == 0){
				return;
			}

			Log("CheckChangesForSubscribedItems ("+OnChangeCallbacksDict.Count.ToString()+")");
			

			foreach(var kv in OnChangeCallbacksDict){
				var item = kv.Key;
				var callbacks = kv.Value;
				
				switch(item.type){
					case DBXItemType.File:
					FileGetRemoteChanges(item.path, (fileChange) => {
						if(fileChange.changeType != DBXFileChangeType.None){
							foreach(var cb in callbacks){
								cb(new List<DBXFileChange>(){fileChange});
							}
						}						
					}, (errorStr) => {
						LogError("Failed to check file changes: "+errorStr);
					}, saveChangesInfoLocally: true);
					break;
					case DBXItemType.Folder:
					FolderGetRemoteChanges(item.path, (res) => {
						if(!res.error){
							if(res.data.Count > 0){
								foreach(var cb in callbacks){
									cb(res.data);
								}
							}
								
						}else{
							LogError("Failed to check folder changes: "+res.errorDescription);
						}
					}, saveChangesInfoLocally: true);
					break;
					default:
					break;
				}
			}
		}

		public void SubscribeToFileChanges(string dropboxFilePath, Action<DBXFileChange> onChange){
			var item = new DBXFile(dropboxFilePath);
			SubscribeToChanges(item, (changes) => {
				onChange(changes[0]);
			});
		}

		public void SubscribeToFolderChanges(string dropboxFolderPath, Action<List<DBXFileChange>> onChange){
			var item = new DBXFolder(dropboxFolderPath);
			SubscribeToChanges(item, onChange);
		}

		void SubscribeToChanges(DBXItem item, Action<List<DBXFileChange>> onChange){
			if(!OnChangeCallbacksDict.ContainsKey(item)){
				// create new list for callbacks
				OnChangeCallbacksDict.Add(item, new List<Action<List<DBXFileChange>>>());
			}

			OnChangeCallbacksDict[item].Add(onChange);			
		}
			

		public void UnsubscribeAllFromChangesForPath(string dropboxPath){
			dropboxPath = DropboxSyncUtils.NormalizePath(dropboxPath);

			var removeKeys = OnChangeCallbacksDict.Where(p => p.Key.path == dropboxPath).Select(p => p.Key).ToList();
			foreach(var k in removeKeys){
				OnChangeCallbacksDict.Remove(k);
			}
		}

		public void UnsubscribeFromChanges(string dropboxPath, Action<List<DBXFileChange>> onChange){
			dropboxPath = DropboxSyncUtils.NormalizePath(dropboxPath);

			var item = OnChangeCallbacksDict.Where(p => p.Key.path == dropboxPath).Select(p => p.Key).FirstOrDefault();
			if(item != null){
				OnChangeCallbacksDict[item].Remove(onChange);
			}
		}
		
	}
}
