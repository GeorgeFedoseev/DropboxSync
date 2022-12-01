using System.IO;
using UnityEngine;

namespace DBXSync {

    public class DropboxSyncConfiguration {
        public static string METADATA_EXTENSION = ".dbxsync";
        public static string INTERMEDIATE_DOWNLOAD_FILE_EXTENSION = ".download";


        public string appKey;
        public string appSecret;
        public string accessToken;
        public string cacheDirecoryPath;

        // TRANSFERS
        public int transferBufferSizeBytes = 16384;
        public int maxSimultaneousDownloadFileTransfers = 3;
        public int maxSimultaneousUploadFileTransfers = 3;
        public int chunkTransferMaxFailedAttempts = 3;

        public int speedTrackerSampleSize = 50;
        public int speedTrackerSampleIntervalMilliseconds = 500;

        // downloading
        public long downloadChunkSizeBytes = 100000000; // 100MB
        public int downloadChunkReadTimeoutMilliseconds = 5000;

        // uploading
        public long uploadChunkSizeBytes = 100000000; // 100MB
        public int uploadRequestWriteTimeoutMilliseconds = 5000;
        public int lightRequestTimeoutMilliseconds = 1000;

        // DELAYS
        public int pathSubscriptionFailedDelaySeconds = 5;
        public int requestErrorRetryDelaySeconds = 3;
        public int chunkTransferErrorRetryDelaySeconds = 7;


        public void SetAccessToken(string accessToken) {
            if (!Utils.IsAccessTokenValid(accessToken)) {
                throw new InvalidConfigurationException($"Dropbox accessToken is not valid ('{accessToken}')");
            }

            this.accessToken = accessToken;
        }

        public void InvalidateAccessToken() {
            this.accessToken = null;
        }

        public void FillDefaultsAndValidate() {
            if (!Utils.IsAppKeyValid(appKey)) {
                throw new InvalidConfigurationException($"Dropbox appKey is not valid ('{appKey}')");
            }
            if (!Utils.IsAppSecretValid(appSecret)) {
                throw new InvalidConfigurationException($"Dropbox appSecret is not valid ('{appSecret}')");
            }


            // set default cache dir path if null
            if (cacheDirecoryPath == null) {
                cacheDirecoryPath = Path.Combine(UnityEngine.Application.persistentDataPath, appKey);
            }

            if (!Directory.Exists(cacheDirecoryPath)) {
                Utils.EnsurePathFoldersExist(cacheDirecoryPath);
            }
        }
    }

}