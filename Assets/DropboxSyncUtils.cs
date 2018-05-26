

using System;
using System.Linq;

namespace DropboxSync.Utils {

    public class DropboxSyncUtils {
        public static string NormalizePath(string strPath){	
			strPath = strPath.Trim();	
			var components = strPath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
			return ("/"+string.Join("/", components)).ToLower();
		}

		public static bool IsPathImmediateChildOfFolder(string folderPath, string candidatePath){
			if(folderPath == candidatePath){
				return false;
			}
			if (candidatePath.IndexOf(folderPath) != 0){
				return false;
			}
			// consider /rootfolder and /rootfolder/file.jpg or /rootfolder/otherfolder
			// replacing gives: /file.jpg /otherfolder
			// so count of slashes should be 1 or 0 (for root folder /)
			return candidatePath.Replace(folderPath, "").Count(c => c == '/') <= 1;			
		}
    }


}