// DropboxSync v2.1
// Created by George Fedoseev 2018-2019

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

		// CACHING 

		string CacheFolderPathForToken {
			get {
				DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);

				var accessTokeFirst5Characters = DropboxAccessToken.Substring(0, 5);
				return Path.Combine(_PersistentDataPath, accessTokeFirst5Characters);
			}		
		}

		void DeleteFileFromCache(string dropboxPath){
			Log("DeleteFileFromCache: "+dropboxPath);
			var localFilePath = GetPathInCache(dropboxPath);
			if(File.Exists(localFilePath)){
				File.Delete(localFilePath);
			}		
		}

		void DownloadToCache (string dropboxPath, Action onSuccess, Action<float> onProgress, Action<DBXError> onError){
				Log("DownloadToCache "+dropboxPath);

				var filePathInCache = GetPathInCache(dropboxPath);
				var localFilePath = GetPathInCache(dropboxPath);
				// make sure containing directory exists
				var fileDirectoryPath = Path.GetDirectoryName(localFilePath);				
				Directory.CreateDirectory(fileDirectoryPath);

				var prms = new DropboxDownloadFileRequestParams(dropboxPath);

				MakeDropboxDownloadRequest(DOWNLOAD_FILE_ENDPOINT, filePathInCache, prms,
					onResponse: (fileMetadata) => {			
						// write metadata
						SaveFileMetadata(fileMetadata);

						onSuccess();
					},
					onProgress: onProgress,
					onWebError: onError
				);				
		}

		bool IsFileCached(string dropboxPath){
			var metadata = GetLocalMetadataForFile(dropboxPath);
			var localFilePath = GetPathInCache(dropboxPath);
			if(metadata != null){
				if(File.Exists(localFilePath)){
					return metadata.filesize == new FileInfo(localFilePath).Length;
				}
			}
			return false;
		}

		string GetPathInCache(string dropboxPath){
			var relativeDropboxPath = dropboxPath.Substring(1);			
			if(relativeDropboxPath.Last() == '/'){
				relativeDropboxPath = relativeDropboxPath.Substring(relativeDropboxPath.Length-1);
			}
			var fullPath = Path.Combine(CacheFolderPathForToken, relativeDropboxPath);
			// replace slashes with backslashes if needed
			fullPath = Path.GetFullPath(fullPath);
			return fullPath;
		}	

		string GetMetadataFilePath(string dropboxPath){
			return GetPathInCache(dropboxPath)+".dbxsync";
		}
	}
}
