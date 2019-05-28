using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public class CacheManager {

        private DropboxSyncConfiguration _config;
        private TransferManager _transferManager;

        public CacheManager(TransferManager transferManager, DropboxSyncConfiguration config){
            _config = config;
            _transferManager = transferManager;
        }

        public async Task<string> GetLocalFilePathAsync(string dropboxPath, IProgress<int> progressCallback) {
            await MaybeCacheFileAsync(dropboxPath, progressCallback);
            return Utils.DropboxPathToLocalPath(dropboxPath, _config);
        }

        private async Task MaybeCacheFileAsync(string dropboxPath, IProgress<int> progressCallback){
            var remoteMetadata = (await new GetFileMetadataRequest (new GetMetadataRequestParameters {
                    path = dropboxPath
                }, _config).ExecuteAsync ()).GetMetadata ();            

            // decide if need to download a new version
            if(!ShouldUpdateFileFromDropbox(remoteMetadata)){
                progressCallback.Report(100);
                return;
            }
            
            // download file
            var downloadToPath = Utils.DropboxPathToLocalPath(dropboxPath, _config);
            remoteMetadata = await _transferManager.DownloadFileAsync(remoteMetadata, downloadToPath, progressCallback);

            // write metadata if all went good
            WriteMetadata(remoteMetadata);
        }

        private bool ShouldUpdateFileFromDropbox(FileMetadata remoteMetadata){
            // check if server has different version
            var localMetadata = GetLocalMetadataForDropboxPath(remoteMetadata.path_lower);

            // If content_hash is different we download.
            // Because we provide only one-way sync: Dropbox -> application
            // and user should not modify files in cache folder.            
            return localMetadata == null || localMetadata.content_hash != remoteMetadata.content_hash;
        }

        private FileMetadata GetLocalMetadataForDropboxPath(string dropboxPath){
            var localMetadataFilePath = Utils.GetMetadataLocalFilePath(dropboxPath, _config);			
			if(File.Exists(localMetadataFilePath)){				
				var metadataJSONString = File.ReadAllText(localMetadataFilePath);
				try {
					return JsonUtility.FromJson<FileMetadata>(metadataJSONString);
				}catch{
					return null;
				}		
			}
			return null;
        }

        private void WriteMetadata(FileMetadata metadata){
            var localMetadataFilePath = Utils.GetMetadataLocalFilePath(metadata.path_lower, _config);			
			// make sure containing directory exists
			Utils.EnsurePathFoldersExist(localMetadataFilePath);			
			File.WriteAllText(localMetadataFilePath, JsonUtility.ToJson(metadata));
        }
    }
}