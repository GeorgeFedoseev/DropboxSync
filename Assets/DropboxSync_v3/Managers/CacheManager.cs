using System;
using System.IO;
using System.Linq;
using System.Threading;
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

        public async Task<string> GetLocalFilePathAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken) {
            await MaybeCacheFileAsync(dropboxPath, progressCallback, cancellationToken);
            return Utils.DropboxPathToLocalPath(dropboxPath, _config);
        }

        private async Task MaybeCacheFileAsync(string dropboxPath, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken){
            var remoteMetadata = (await new GetMetadataRequest (new GetMetadataRequestParameters {
                    path = dropboxPath
                }, _config).ExecuteAsync ()).GetMetadata ();            

            await MaybeCacheFileAsync(remoteMetadata, progressCallback, cancellationToken);
        }

        private async Task MaybeCacheFileAsync(Metadata remoteMetadata, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken){
            var localFilePath = Utils.DropboxPathToLocalPath(remoteMetadata.path_lower, _config);

            // Debug.LogWarning($"MaybeCacheFileAsync {remoteMetadata.path_display}");

            // decide if need to download a new version
            if(File.Exists(localFilePath) && !ShouldUpdateFileFromDropbox(remoteMetadata)){
                // Debug.LogWarning($"MaybeCacheFileAsync: don't download {remoteMetadata.path_display}");
                return;
            }
            
            // download file            
            remoteMetadata = await _transferManager.DownloadFileAsync(remoteMetadata, localFilePath, progressCallback, cancellationToken);

            // write metadata if all went good
            WriteMetadata(remoteMetadata);
        }

        public bool HaveFileLocally(Metadata remoteMetadata){
            return HaveFileLocally(remoteMetadata.path_lower);
        }

        public bool HaveFileLocally(string dropboxPath){
            return GetLocalMetadataForDropboxPath(dropboxPath) != null;
        }

        public void RemoveFileFromCache(Metadata metadata){
            if(HaveFileLocally(metadata)){
                var localPath = Utils.DropboxPathToLocalPath(metadata.path_lower, _config);
                var localMetadataFilePath = Utils.GetMetadataLocalFilePath(metadata.path_lower, _config); 
                // delete file
                File.Delete(localPath);
                // delete metadata
                File.Delete(localMetadataFilePath);
            } 
        }

        public async Task SyncChangeAsync(EntryChange entryChange, Progress<TransferProgressReport> progressCallback, CancellationToken? cancellationToken){
            
            switch(entryChange.type){
                case EntryChangeType.Created:                
                case EntryChangeType.Modified:
                // remove from current downloads or queue in TransferManager
                // Debug.LogWarning($"Waiting for cancellation of {entryChange.metadata.path_display}...");
                await _transferManager.CancelQueuedOrExecutingDownloadTransferAsync(entryChange.metadata.path_lower);
                // Debug.LogWarning($"Re-download file {entryChange.metadata.path_display}");
                // then start download again
                await MaybeCacheFileAsync(entryChange.metadata, progressCallback, cancellationToken);
                break;
                case EntryChangeType.Removed:
                // remove from queue or current downloads in TransferManager
                await _transferManager.CancelQueuedOrExecutingDownloadTransferAsync(entryChange.metadata.path_lower);
                // remove locally
                RemoveFileFromCache(entryChange.metadata);     
                break;
            }
        }
        

        public bool ShouldUpdateFileFromDropbox(Metadata remoteMetadata){
            // check if server has different version
            var localMetadata = GetLocalMetadataForDropboxPath(remoteMetadata.path_lower);

            // If content_hash is different we download.
            // Because we provide only one-way sync: Dropbox -> application
            // and user should not modify files in cache folder.            
            return localMetadata == null || localMetadata.content_hash != remoteMetadata.content_hash;
        }

        public Metadata GetLocalMetadataForDropboxPath(string dropboxPath){
            var localMetadataFilePath = Utils.GetMetadataLocalFilePath(dropboxPath, _config);			
			if(File.Exists(localMetadataFilePath)){				
				var metadataJSONString = File.ReadAllText(localMetadataFilePath);
				try {
					return JsonUtility.FromJson<Metadata>(metadataJSONString);
				}catch{
					return null;
				}		
			}
			return null;
        }

        private void WriteMetadata(Metadata metadata){
            var localMetadataFilePath = Utils.GetMetadataLocalFilePath(metadata.path_lower, _config);			
			// make sure containing directory exists
			Utils.EnsurePathFoldersExist(localMetadataFilePath);			
			File.WriteAllText(localMetadataFilePath, JsonUtility.ToJson(metadata));            
        }
    }
}