// DropboxSync v2.0
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
		private static readonly string UPLOAD_FILE_ENDPOINT = "https://content.dropboxapi.com/2/files/upload";

		// UPLOADING FILE

		public void UploadFile(string dropboxPath, string localFilePath, Action<DropboxRequestResult<DBXFile>> onResult,
									 Action<float> onProgress = null) 
		{

			// chec if specified local file path exists
			if(!File.Exists(localFilePath)){
				onResult(DropboxRequestResult<DBXFile>.Error(
							new DBXError("Local file "+localFilePath+" does not exist.", DBXErrorType.LocalPathNotFound)
						)
				);
				return;
			}


			// read file bytes
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


			// upload that bytes
			UploadFile(dropboxPath, fileBytes, onResult, onProgress);
		}

		public void UploadFile(string dropboxPath, byte[] bytes, Action<DropboxRequestResult<DBXFile>> onResult,
										 Action<float> onProgress = null) 
		{
			var prms = new DropboxUploadFileRequestParams(dropboxPath);
			MakeDropboxUploadRequest(UPLOAD_FILE_ENDPOINT, bytes, prms,
			onResponse: (fileMetadata) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(new DropboxRequestResult<DBXFile>(fileMetadata));	
				});				
			},
			onProgress: (progress) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onProgress(progress);
				});
			},
			onWebError: (webErrorStr) => {
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(DropboxRequestResult<DBXFile>.Error(webErrorStr));
				});					
			});
		}
		
		
	}
}
