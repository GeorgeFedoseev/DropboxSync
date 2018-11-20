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

		// METADATA

		void SaveFileMetadata(DBXFile fileMetadata){		
			
			var localFilePath = GetPathInCache(fileMetadata.path);		
			
			// make sure containing directory exists
			var fileDirectoryPath = Path.GetDirectoryName(localFilePath);
			//Log("Local cached directory path: "+fileDirectoryPath);
			Directory.CreateDirectory(fileDirectoryPath);

			// write metadata to separate file near
			var newMetadataFilePath = GetMetadataFilePath(fileMetadata.path);
			File.WriteAllText(newMetadataFilePath, JsonUtility.ToJson(fileMetadata));
			//Log("Wrote metadata file "+newMetadataFilePath);
		}

		DBXFile GetLocalMetadataForFile(string dropboxFilePath){
			var metadataFilePath = GetMetadataFilePath(dropboxFilePath);
			return ParseLocalMetadata(metadataFilePath);
		}

		DBXFile ParseLocalMetadata(string localMetadataPath){
			if(File.Exists(localMetadataPath)){
				// get local content hash
				var fileJsonStr = File.ReadAllText(localMetadataPath);				
				
				try {
					return JsonUtility.FromJson<DBXFile>(fileJsonStr);					
				}catch{
					return null;
				}		
			}
			return null;
		}	
		
	}
}
