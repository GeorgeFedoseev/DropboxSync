using System;
using System.Collections.Generic;


namespace DBXSync.Model {

	


	[Serializable]
	public class DropboxRequestParams {

	}

	[Serializable]
	public class DropboxListFolderRequestParams : DropboxRequestParams {
		public string path;
		public bool recursive = true;
		public bool include_media_info = false;
		public bool include_deleted = false;
		public bool include_has_explicit_shared_members = false;
		public bool include_mounted_folders = false;
	}

	[Serializable]
	public class DropboxContinueWithCursorRequestParams : DropboxRequestParams {
		public string cursor;

		public DropboxContinueWithCursorRequestParams(string cur) {
			cursor = cur;
		}
	}



	[Serializable]
	public class DropboxGetMetadataRequestParams : DropboxRequestParams {
		public string path;	
		public bool include_media_info = false;
		public bool include_deleted = false;
		public bool include_has_explicit_shared_members = false;	

		public DropboxGetMetadataRequestParams(string _path){
			path = _path;
		}
	}


	[Serializable]
	public class DropboxDownloadFileRequestParams : DropboxRequestParams {
		public string path;

		public DropboxDownloadFileRequestParams(string _path){
			path = _path;
		}
	}

	[Serializable]
	public class DropboxUploadFileRequestParams : DropboxRequestParams {
		public string path;
		public string mode;


		public DropboxUploadFileRequestParams(string _path){
			path = _path;
			mode = "overwrite";
		}
	}


	public class DropboxRequestResult<T> {
		public T data;
		public DBXError error = null;

		public DropboxRequestResult(T res){
			this.data = res;
		}

		public static DropboxRequestResult<T> Error(string errorDescription, DBXErrorType errorType = DBXErrorType.Unknown){
			var inst = new DropboxRequestResult<T>(default(T));
			inst.error = new DBXError(errorDescription, errorType);
			return inst;
		}
	}

	public class DropboxFileDownloadRequestResult<T> {
		public T data;
		public DBXFile fileMetadata;
		public DBXError error = null;


		public DropboxFileDownloadRequestResult(T res, DBXFile metadata){
			this.data = res;
			fileMetadata = metadata;
		}

		public static DropboxFileDownloadRequestResult<T> Error(string errorDescription, DBXErrorType errorType = DBXErrorType.Unknown){
			var inst = new DropboxFileDownloadRequestResult<T>(default(T), null);
			inst.error = new DBXError(errorDescription, errorType);
			return inst;
		}
	}
}