

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBXSync {

    public static class Utils {


        public static bool AreEqualDropboxPaths(string dropboxPath1, string dropboxPath2){
            return UnifyDropboxPath(dropboxPath1) == UnifyDropboxPath(dropboxPath2);
        }

        private static string UnifyDropboxPath(string dropboxPath){
            dropboxPath = dropboxPath.Trim();
            dropboxPath = dropboxPath.ToLower();
            
            if(dropboxPath.First() != '/'){
                dropboxPath = $"/{dropboxPath}";
            }			
			if(dropboxPath.Last() == '/'){
				dropboxPath = dropboxPath.Substring(1, dropboxPath.Length-1);
			}

            return dropboxPath;
        }

        public static string GetMetadataLocalFilePath(string dropboxPath, DropboxSyncConfiguration config){
            dropboxPath = UnifyDropboxPath(dropboxPath);
			return DropboxPathToLocalPath(dropboxPath, config)+".dbxsync";
		}

        public static string DropboxPathToLocalPath(string dropboxPath, DropboxSyncConfiguration config){
            string relativeDropboxPath = UnifyDropboxPath(dropboxPath);

            if(relativeDropboxPath.First() == '/'){
                relativeDropboxPath = relativeDropboxPath.Substring(1);
            }			

			var fullPath = Path.Combine(config.cacheDirecoryPath, relativeDropboxPath);
			// replace slashes with backslashes if needed
			fullPath = Path.GetFullPath(fullPath);

			return fullPath;
		}	

        public static string GetDownloadTempFilePath(string targetLocalPath, string content_hash){
            string piece_of_hash = content_hash.Substring(0, 10);
            return $"{targetLocalPath}.{piece_of_hash}.download";                   
        }

        public static bool IsAccessTokenValid(string accessToken) {
            if(accessToken == null) {
                return false;
            }
            
            if(accessToken.Trim().Length == 0){
                return false;
            }

            if(accessToken.Length < 20){
                return false;
            }

            return true;
        }


        public static string FixDropboxJSONString(string jsonStr) {
            
            jsonStr = jsonStr.Replace("\".tag\"", "\"tag\"");

            return jsonStr;
        }

        public static void EnsurePathFoldersExist(string path){
            var dirPath = Path.GetDirectoryName(path);				
			Directory.CreateDirectory(dirPath);
        }

        public static IEnumerable<long> LongRange(long start, long count){
            var limit = start + count;

            while (start < limit)
            {
                yield return start;
                start++;
            }
        }

    }

}