

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBXSync {

    public static class Utils {

        public static string DropboxPathToLocalPath(string dropboxPath, DropboxSyncConfiguration config){
            string relativeDropboxPath = dropboxPath;
            if(relativeDropboxPath.First() == '/'){
                relativeDropboxPath = relativeDropboxPath.Substring(1);
            }			
			if(relativeDropboxPath.Last() == '/'){
				relativeDropboxPath = relativeDropboxPath.Substring(1, relativeDropboxPath.Length-1);
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