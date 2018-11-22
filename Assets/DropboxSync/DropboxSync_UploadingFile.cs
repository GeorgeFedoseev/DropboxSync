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

		// UPLOADING FILE

		public void UploadFile(string dropboxPath, string localFilePath, Action<DropboxRequestResult<DBXFile>> onResult,
									 Action<float> onProgress = null) 
		{
			if(!File.Exists(localFilePath)){
				onResult(DropboxRequestResult<DBXFile>.Error(
							new DBXError("Local file "+localFilePath+" does not exist.", DBXErrorType.FileNotFound)
						)
				);
				return;
			}

			byte[] fileBytes = null;
			try{
				fileBytes = File.ReadAllBytes(localFilePath);
			}catch(Exception ex){
				onResult(DropboxRequestResult<DBXFile>.Error(
							new DBXError("Failed to read local file "+localFilePath+": "+ex.Message, DBXErrorType.LocalFileSystemError)
					)
				);
				return;
			}

			UploadFile(dropboxPath, fileBytes, onResult, onProgress);
		}

		public void UploadFile(string dropboxPath, byte[] bytes, Action<DropboxRequestResult<DBXFile>> onResult,
										 Action<float> onProgress = null) 
		{
			var prms = new DropboxUploadFileRequestParams(dropboxPath);
			MakeDropboxUploadRequest("https://content.dropboxapi.com/2/files/upload", bytes, prms,
			onResponse: (fileMetadata) => {
				onResult(new DropboxRequestResult<DBXFile>(fileMetadata));
			},
			onProgress: onProgress,
			onWebError: (webErrorStr) => {
				onResult(DropboxRequestResult<DBXFile>.Error(webErrorStr));
			});
		}
		
		
	}
}
