

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DBXSync {

    public static class Utils {        

        public static T ConvertBytesTo<T>(byte[] bytes) where T:class {
            if(typeof(T) == typeof(string)){
                // TEXT DATA
				return GetAutoDetectedEncodingStringFromBytes(bytes) as T;				
			} else if(typeof(T) == typeof(Texture2D)){				
				// IMAGE DATA
                return LoadImageToTexture2D(bytes) as T;				
			} else{
                // TRY DESERIALIZE JSON
                var str = GetAutoDetectedEncodingStringFromBytes(bytes);
                return JsonUtility.FromJson<T>(str);                
            }
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

        public static string GetAutoDetectedEncodingStringFromBytes(byte[] bytes){
            using (var reader = new System.IO.StreamReader(new System.IO.MemoryStream(bytes), true)){
                var detectedEncoding = reader.CurrentEncoding;
                return detectedEncoding.GetString(bytes);
            }	
        }


        public static async Task RethrowDropboxHttpRequestException(HttpRequestException ex, HttpResponseMessage responseMessage,  RequestParameters parameters, string endpoint){
            throw await DecorateDropboxHttpRequestException(ex, responseMessage, parameters, endpoint);
        }

        public static async Task<Exception> DecorateDropboxHttpRequestException(HttpRequestException ex, HttpResponseMessage responseMessage, RequestParameters parameters, string endpoint){
            Exception result = ex;
            try {
                var errorResponseString = await responseMessage.Content.ReadAsStringAsync();

                if(!string.IsNullOrWhiteSpace(errorResponseString)){
                    try {
                        var errorResponse = JsonUtility.FromJson<Response>(errorResponseString);
                        if(!string.IsNullOrEmpty(errorResponse.error_summary)){
                            if(errorResponse.error.tag == "reset"){
                                result = new DropboxResetCursorAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            }else if(errorResponse.error_summary.Contains("not_found")){
                                result = new DropboxNotFoundAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            }else{
                                result = new DropboxAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            }
                            
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
                
            }catch{
                // failed to read response message
                result = ex;
            }

            return result;
        }

        // public static void RethrowDropboxRequestWebException(WebException ex, RequestParameters parameters, string endpoint){
        //     throw DecorateDropboxRequestWebException(ex, parameters, endpoint);
        // }

        // public static Exception DecorateDropboxRequestWebException(WebException ex, RequestParameters parameters, string endpoint){
        //     Exception result = ex;    

        //     // Debug.Log($"Decorate exception. Exception message: {ex.Message}");
        //     if(ex.Message == "The request was canceled." || ex.Message == "Aborted."){
        //         return new OperationCanceledException("Request was cancelled");
        //     }
                    
        //     try {
        //         var errorResponseString = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();  

        //         if(!string.IsNullOrWhiteSpace(errorResponseString)){
        //             try {
        //                 var errorResponse = JsonUtility.FromJson<Response>(errorResponseString);
        //                 if(!string.IsNullOrEmpty(errorResponse.error_summary)){
        //                     if(errorResponse.error.tag == "reset"){
        //                         result = new DropboxResetCursorAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
        //                                                              errorResponse.error_summary, errorResponse.error.tag);
        //                     }else if(errorResponse.error.tag == "not_found"){
        //                         result = new DropboxNotFoundAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
        //                                                              errorResponse.error_summary, errorResponse.error.tag);
        //                     }else{
        //                         result = new DropboxAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
        //                                                              errorResponse.error_summary, errorResponse.error.tag);
        //                     }
                            
        //                 }else{
        //                     // empty error-summary
        //                     result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);                                            
        //                 }
        //             }catch {
        //                 // not json-formatted error
        //                 result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);                                        
        //             }
        //         }else{
        //             // no text in response - throw original
        //             result = ex;
        //         }                        
        //     } catch {
        //         // failed to get response - throw original
        //         result = ex;
        //     }   

        //     return result;
        // }

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

            // remove slash in the end		
			if(dropboxPath.Last() == '/'){
				dropboxPath = dropboxPath.Substring(1, dropboxPath.Length-1);
			}

            // API: 'Specify the root folder as an empty string rather than as "/"'
            if(dropboxPath == "/"){
                dropboxPath = "";
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