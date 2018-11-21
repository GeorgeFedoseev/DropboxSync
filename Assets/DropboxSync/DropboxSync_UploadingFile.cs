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

		public void UploadFile<T>(string dropboxPath, T fileData, Action<DropboxRequestResult<DBXFile>> onResult, Action<float> onProgress = null)  where T : class {

		}

		public void UploadFile(string dropboxPath, byte[] bytes, Action<DropboxRequestResult<DBXFile>> onResult, Action<float> onProgress = null) {
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
