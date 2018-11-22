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

		// GETTING REMOTE CHANGES

		void FolderGetRemoteChanges(string dropboxFolderPath, Action<DropboxRequestResult<List<DBXFileChange>>> onResult, bool saveChangesInfoLocally = false){
			GetFolderItems(dropboxFolderPath, 
			onResult: (res) => {
				if(res.error != null){
					onResult(DropboxRequestResult<List<DBXFileChange>>.Error(res.error));
				}else{
					var fileChanges = new List<DBXFileChange>();

					foreach(DBXFile remoteMetadata in res.data.Where(x => x.type == DBXItemType.File)){
						var localMetadata = GetLocalMetadataForFile(remoteMetadata.path);
						if(localMetadata != null && !localMetadata.deletedOnRemote){
							if(localMetadata.contentHash != remoteMetadata.contentHash){
								fileChanges.Add(new DBXFileChange(remoteMetadata, DBXFileChangeType.Modified));						
							}
						}else{
							// no local metadata for this remote path - new object
							fileChanges.Add(new DBXFileChange(remoteMetadata, DBXFileChangeType.Added));
						}						
					}

					// find other local files which were not in remote response (find deleted on remote files)
					var processedDropboxFilePaths = res.data.Where(x => x.type == DBXItemType.File).Select(x => x.path).ToList();

					//Log("Find all metadata paths");
					var localDirectoryPath = GetPathInCache(dropboxFolderPath);
					if(Directory.Exists(localDirectoryPath)){
						foreach (string localMetadataFilePath in Directory.GetFiles(localDirectoryPath, "*.dbxsync", SearchOption.AllDirectories)){							
							var metadata = ParseLocalMetadata(localMetadataFilePath);
							var dropboxPath = metadata.path;
							if(!processedDropboxFilePaths.Contains(dropboxPath) && !metadata.deletedOnRemote){
								// wasnt in remote data - means removed
								fileChanges.Add(new DBXFileChange(DBXFile.DeletedOnRemote(dropboxPath), DBXFileChangeType.Deleted));
							}
						}
					}

					if(saveChangesInfoLocally){
						foreach(var fc in fileChanges){
							SaveFileMetadata(fc.file);
						}
					}

					onResult(new DropboxRequestResult<List<DBXFileChange>>(fileChanges));
				}
			}, recursive:true, onProgress:null);
		}

		void FileGetRemoteChanges(string dropboxFilePath, Action<DBXFileChange> onResult, Action<DBXError> onError, bool saveChangesInfoLocally = false){
			var localFilePath = GetPathInCache(dropboxFilePath);
			var metadataFilePath = GetMetadataFilePath(dropboxFilePath);			

			var localMetadata = GetLocalMetadataForFile(dropboxFilePath);
				
			// request for metadata to get remote content hash
			//Log("Getting metadata");
			GetFileMetadata(dropboxFilePath, onResult: (res) => {
				Log("Got metadata for file "+dropboxFilePath);
				DBXFileChange result = null;

				if(res.error != null){
					if (res.error.ErrorType == DBXErrorType.FileNotFound){
						//Log("file not found");
						// file was deleted or moved

						// if we knew about this file before
						if(localMetadata != null){
							// if we didnt know that it was removed
							if(!localMetadata.deletedOnRemote){
								result = new DBXFileChange(DBXFile.DeletedOnRemote(dropboxFilePath), DBXFileChangeType.Deleted);
							}else{
								// no change								
								result = new DBXFileChange(localMetadata, DBXFileChangeType.None);
							}
						}else{
							onError(res.error);
						}
						
					}else{
						onError(res.error);
						return;
					}									
				}else{
					//Log("Got metadata");
					var remoteMedatadata = res.data;

					if(localMetadata != null && !localMetadata.deletedOnRemote){
						// get local content hash				
						var local_content_hash = localMetadata.contentHash;

						var remote_content_hash = remoteMedatadata.contentHash;

						if(local_content_hash != remote_content_hash){
							result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Modified);
						}else{
							result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.None);						
						}	
					}else{						
						// metadata file doesnt exist
						// TODO: check maybe file itself exists and right version, then just create metadata file - no need to redownload file itself
						result = new DBXFileChange(remoteMedatadata, DBXFileChangeType.Added);
					}
				}

				// if no error
				if(result != null){
					if(saveChangesInfoLocally){			

						SaveFileMetadata(result.file);
					}
					
					onResult(result);
				}
			});
		}
		
	}
}
