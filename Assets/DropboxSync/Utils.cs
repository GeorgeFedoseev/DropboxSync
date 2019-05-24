

using System.Collections.Generic;
using System.IO;

namespace DBXSync {

    public static class Utils {

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