using System.IO;

namespace DBXSync {
    
    public class DropboxSyncConfiguration {
        public static string METADATA_EXTENSION = ".dbxsync";
        public static string INTERMEDIATE_DOWNLOAD_FILE_EXTENSION = ".download";


        public string accessToken;
        public string cacheDirecoryPath;

        public int transferBufferSizeBytes = 8192;

        public int pathSubscriptionFailedDelaySeconds = 5;

        // retry chunk transfers
        public int chunkTransferMaxFailedAttempts = 3;
        public int requestErrorRetryDelaySeconds = 3;

        // requests
        public int lightRequestTimeoutMilliseconds = 1000;
        public int uploadRequestWriteTimeoutMilliseconds = 5000;

        // downloading
        public long downloadChunkSizeBytes = 10000000; // TODO: change to bigger
        public int downloadChunkReadTimeoutMilliseconds = 5000;
        public int downloadChunckedThreadNum = 2;

        // uploading
        public long uploadChunkSizeBytes = 150000000;
        

        // transfers
        public int maxSimultaneousDownloadFileTransfers = 3;
        public int maxSimultaneousUploadFileTransfers = 3;

        public void FillDefaultsAndValidate(){
            if(!Utils.IsAccessTokenValid(accessToken)) {
                throw new InvalidConfigurationException($"Dropbox accessToken is not valid ('{accessToken}')");
            }

            // set default cache dir path if null
            if(cacheDirecoryPath == null) {
                var accessTokeFirst5Characters = accessToken.Substring(0, 5);
				cacheDirecoryPath = Path.Combine(UnityEngine.Application.persistentDataPath, accessTokeFirst5Characters);
            }

            if(!Directory.Exists(cacheDirecoryPath)) {
                Utils.EnsurePathFoldersExist(cacheDirecoryPath);
                //throw new InvalidConfigurationException($"Cache directory path ({cacheDirecoryPath}) does not exist");
            }
        }
    }

}