

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public static class Utils {        

        


        public static void RethrowDropboxRequestWebException(WebException ex, RequestParameters parameters, string endpoint){
            throw DecorateDropboxRequestWebException(ex, parameters, endpoint);
        }

        public static Exception DecorateDropboxRequestWebException(WebException ex, RequestParameters parameters, string endpoint){
            Exception result = ex;    

            // Debug.Log($"Decorate exception. Exception message: {ex.Message}");
            if(ex.Message == "The request was canceled." || ex.Message == "Aborted."){
                return new OperationCanceledException("Request was cancelled");
            }
                    
            try {
                var errorResponseString = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();  

                if(!string.IsNullOrWhiteSpace(errorResponseString)){
                    try {
                        var errorResponse = JsonUtility.FromJson<Response>(errorResponseString);
                        if(!string.IsNullOrEmpty(errorResponse.error_summary)){
                            result = new DropboxAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                        }else{
                            // empty error-summary
                            result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);                                            
                        }
                    }catch {
                        // not json-formatted error
                        result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);                                        
                    }
                }else{
                    // no text in response - throw original
                    result = ex;
                }                        
            } catch {
                // failed to get response - throw original
                result = ex;
            }   

            return result;
        }

        public static bool AreEqualDropboxPaths(string dropboxPath1, string dropboxPath2){
            return UnifyDropboxPath(dropboxPath1) == UnifyDropboxPath(dropboxPath2);
        }

        public static string UnifyDropboxPath(string dropboxPath){
            dropboxPath = dropboxPath.Trim();

            // lowercase
            dropboxPath = dropboxPath.ToLower();
            
            // always slash in the beginning
            if(dropboxPath.First() != '/'){
                dropboxPath = $"/{dropboxPath}";
            }

            // remove slash in the end for folders			
			if(dropboxPath.Last() == '/'){
				dropboxPath = dropboxPath.Substring(1, dropboxPath.Length-1);
			}

            return dropboxPath;
        }

        public static string GetMetadataLocalFilePath(string dropboxPath, DropboxSyncConfiguration config){
            dropboxPath = UnifyDropboxPath(dropboxPath);
			return DropboxPathToLocalPath(dropboxPath, config) + DropboxSyncConfiguration.METADATA_EXTENSION;
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
            return $"{targetLocalPath}.{piece_of_hash}{DropboxSyncConfiguration.INTERMEDIATE_DOWNLOAD_FILE_EXTENSION}";
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