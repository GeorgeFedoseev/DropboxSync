

using System;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEngine;

namespace DBXSync.Utils {

    public class DropboxSyncUtils {
        public static string NormalizePath(string strPath){	
			strPath = strPath.Trim();	
			var components = strPath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
			return ("/"+string.Join("/", components)).ToLower();
		}

        public static void ValidateAccessToken(string accessToken){
            if(DropboxSyncUtils.IsBadAccessToken(accessToken)){
                throw new Exception("Bad Dropbox access token. Please specify valid access token.");					
            }
        }

        public static bool IsBadAccessToken(string accessToken){
            if(accessToken.Trim().Length == 0){
                return true;
            }

            if(accessToken.Length < 20){
                return true;
            }

            return false;
        }

        public static bool IsBadDropboxPath(string dropboxPath){
            if(dropboxPath.Length == 0){
                return true;
            }

            if(dropboxPath[0] != '/'){
                Debug.LogError("Dropbox paths should start with '/'");
                return true;
            }
            

            return false;
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

        public static Texture2D LoadImageToTexture2D(byte[] data) {
            Texture2D tex = null;
            tex = new Texture2D(2, 2);                     
            
            tex.LoadImage(data);
            //tex.filterMode = FilterMode.Trilinear; 	
            //tex.wrapMode = TextureWrapMode.Clamp;
            //tex.anisoLevel = 9;

            return tex;
        }

        public static string GetAudtoDetectedEncodingStringFromBytes(byte[] bytes){
            using (var reader = new System.IO.StreamReader(new System.IO.MemoryStream(bytes), true)){
                var detectedEncoding = reader.CurrentEncoding;
                return detectedEncoding.GetString(bytes);
            }	
        }

        public static bool IsOnline(){
            try {
                using (WebClient client = new WebClient()){
                    using (client.OpenRead("http://www.google.com/")){
                        return true;
                    }
                }
            }catch{
                return false;
            }
        }

         public static void IsOnlineAsync(Action<bool> onResult){
            var thread = new Thread(() => {                
                onResult(IsOnline());
            });
            thread.IsBackground = true;
            thread.Start();
        }

    }


}