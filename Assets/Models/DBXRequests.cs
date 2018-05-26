using System;
using System.Collections.Generic;


namespace DropboxSync.Model {

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
	public class DropboxGetMetadataParams : DropboxRequestParams {
		public string path;	
		public bool include_media_info = false;
		public bool include_deleted = false;
		public bool include_has_explicit_shared_members = false;	
	}


	[Serializable]
	public class DropboxDownloadFileRequestParams : DropboxRequestParams {
		public string path;

		public DropboxDownloadFileRequestParams(string _path){
			path = _path;
		}
	}


	public class DropboxRequestResult<T> {
		public T data;
		public bool error = false;
		public string errorDescription = null;

		public DropboxRequestResult(T res){
			this.data = res;
		}

		public static DropboxRequestResult<T> Error(string errorDescription){
			var inst = new DropboxRequestResult<T>(default(T));
			inst.error = true;
			inst.errorDescription = errorDescription;
			return inst;
		}
	}
}