using System.IO;

namespace DBXSync {
    
    public class DropboxSyncConfiguration {
        public static string METADATA_EXTENSION = ".dbxsync";
        public static string INTERMEDIATE_DOWNLOAD_FILE_EXTENSION = ".download";


        public string accessToken;
        public string cacheDirecoryPath;

        // TRANSFERS
        public int transferBufferSizeBytes = 16384; // 8KB
        public int maxSimultaneousDownloadFileTransfers = 3;
        public int maxSimultaneousUploadFileTransfers = 3;
        public int chunkTransferMaxFailedAttempts = 3;

        public int speedTrackerSampleSize = 50;
        public int speedTrackerSampleIntervalMilliseconds = 500;

        // downloading
        public long downloadChunkSizeBytes = 100000000; // 100MB
        public int downloadChunkReadTimeoutMilliseconds = 5000;        

        // uploading
        public long uploadChunkSizeBytes = 150000000; // 150MB
        public int uploadRequestWriteTimeoutMilliseconds = 5000;
        public int lightRequestTimeoutMilliseconds = 1000;

        // DELAYS
        public int pathSubscriptionFailedDelaySeconds = 5;
        public int requestErrorRetryDelaySeconds = 3; 
        public int chunkTransferErrorRetryDelaySeconds = 7; 
        

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
            }
        }
    }

}