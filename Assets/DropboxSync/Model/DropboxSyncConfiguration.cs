using System.IO;

namespace DBXSync {
    
    public class DropboxSyncConfiguration {
        public string accessToken;
        public string cacheDirecoryPath;

        // downloading
        public long downloadChunkSizeBytes = 10000000;
        public int downloadChunckedThreadNum = 4;

        // uploading
        public long uploadChunkSizeBytes = 10000000;        

        // transfers
        public int maxSimultaneousDownloadFileTransfers = 3;
        public int maxSimultaneousUploadFileTransfers = 3;

        // public int dropboxReachabilityCheckIntervalMilliseconds = 5000;

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