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
        
        private static readonly string MOVE_FILE_ENDPOINT = "https://api.dropboxapi.com/2/files/move_v2";
	
		// FILE OPERATIONS

        public void MoveFile(string dropboxFromPath, string dropboxToPath,
                                 Action<DropboxRequestResult<DBXFile>> onResult) {
			

			var prms = new DropboxMoveFileRequestParams();
			prms.from_path = dropboxFromPath;
            prms.to_path = dropboxToPath;

			MakeDropboxRequest(MOVE_FILE_ENDPOINT, prms, (jsonStr) => {

				DBXFile fileMetadata = null;

				try {
					var root = JSON.FromJson<Dictionary<string, object>>(jsonStr);
					fileMetadata = DBXFile.FromDropboxDictionary(root["metadata"] as Dictionary<string, object>);
				}catch(Exception ex){
					_mainThreadQueueRunner.QueueOnMainThread(() => {
						onResult(DropboxRequestResult<DBXFile>.Error(new DBXError(ex.Message, DBXErrorType.ParsingError)));
					});
					return;
				}							
				
				_mainThreadQueueRunner.QueueOnMainThread(() => {
					onResult(new DropboxRequestResult<DBXFile>(fileMetadata));
				});				
			}, onProgress: (progress) => {}, (error) => {
				
				_mainThreadQueueRunner.QueueOnMainThread(() => {
                    if(error.ErrorType == DBXErrorType.RemotePathAlreadyExists){
                        error.ErrorDescription = "Can't move file: "+dropboxToPath+" already exists";
                    }
					onResult(DropboxRequestResult<DBXFile>.Error(error));
				});
			});
		}
		
		
	}
}
