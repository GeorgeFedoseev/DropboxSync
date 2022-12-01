

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

        public static T ConvertBytesTo<T>(byte[] bytes) where T : class {
            if (typeof(T) == typeof(string)) {
                // TEXT DATA
                return GetAutoDetectedEncodingStringFromBytes(bytes) as T;
            } else if (typeof(T) == typeof(Texture2D)) {
                // IMAGE DATA
                return LoadImageToTexture2D(bytes) as T;
            } else {
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

        public static string GetAutoDetectedEncodingStringFromBytes(byte[] bytes) {
            using (var reader = new System.IO.StreamReader(new System.IO.MemoryStream(bytes), true)) {
                var detectedEncoding = reader.CurrentEncoding;
                return detectedEncoding.GetString(bytes);
            }
        }


        public static async Task RethrowDropboxHttpRequestException(HttpRequestException ex, HttpResponseMessage responseMessage, RequestParameters parameters, string endpoint) {
            throw await DecorateDropboxHttpRequestException(ex, responseMessage, parameters, endpoint);
        }

        public static async Task<Exception> DecorateDropboxHttpRequestException(HttpRequestException ex, HttpResponseMessage responseMessage, RequestParameters parameters, string endpoint) {
            Exception result = ex;
            try {
                var errorResponseString = await responseMessage.Content.ReadAsStringAsync();

                if (!string.IsNullOrWhiteSpace(errorResponseString)) {
                    try {
                        var errorResponse = GetDropboxResponseFromJSON<Response>(errorResponseString);
                        if (!string.IsNullOrEmpty(errorResponse.error_summary)) {
                            if (errorResponse.error.tag == "reset") {
                                result = new DropboxResetCursorAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            } else if (errorResponse.error.tag == "invalid_access_token" || errorResponse.error.tag == "expired_access_token") {
                                result = new DropboxAccessTokenExpiredAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            } else if (errorResponse.error_summary.Contains("not_found")) {
                                result = new DropboxNotFoundAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            } else {
                                result = new DropboxAPIException($"error: {errorResponse.error_summary}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}",
                                                                     errorResponse.error_summary, errorResponse.error.tag);
                            }

                        } else if (errorResponseString.Contains("error_description")) {
                            try {
                                var error = Utils.GetDropboxResponseFromJSON<ErrorResponse>(errorResponseString);
                                if (error.error == "invalid_grant") {
                                    result = new InvalidGrantTokenException($"{error.error_description}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}");
                                } else {
                                    result = new DropboxAPIException($"error: {error.error}, {error.error_description}; request parameters: {parameters}; endpoint: {endpoint}; full-response: {errorResponseString}");
                                }
                            } catch {
                                result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);
                            }
                        } else {
                            // empty error-summary
                            result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);
                        }
                    } catch {
                        // not json-formatted error
                        result = new DropboxAPIException($"error: {errorResponseString}; request parameters: {parameters}; endpoint: {endpoint}", errorResponseString, null);
                    }
                } else {
                    // no text in response - throw original
                    result = ex;
                }

            } catch {
                // failed to read response message
                result = ex;
            }

            return result;
        }


        public static async Task<T> GetPostResponse<T>(Uri uri, List<KeyValuePair<string, string>> data,
            System.Net.Http.Headers.AuthenticationHeaderValue authenticationHeaderValue = null
        ) {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, uri);
            requestMessage.Headers.Authorization = authenticationHeaderValue;
            requestMessage.Content = new FormUrlEncodedContent(data);

            using (var client = new HttpClient()) {
                var response = await client.SendAsync(requestMessage);
                try {
                    // throw exception if not success status code
                    response.EnsureSuccessStatusCode();
                } catch (HttpRequestException ex) {
                    await Utils.RethrowDropboxHttpRequestException(ex, response, new RequestParameters(), uri.ToString());
                }

                var responseString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseString) || responseString == "null") {
                    Debug.LogError($"[DropboxSync] Failed to get access token: response is null");
                    return default(T);
                }

                return Utils.GetDropboxResponseFromJSON<T>(responseString);
            }
        }

        public static bool AreEqualDropboxPaths(string dropboxPath1, string dropboxPath2) {
            return UnifyDropboxPath(dropboxPath1) == UnifyDropboxPath(dropboxPath2);
        }

        public static string UnifyDropboxPath(string dropboxPath) {
            dropboxPath = dropboxPath.Trim();

            // lowercase
            dropboxPath = dropboxPath.ToLower();

            // always slash in the beginning
            if (dropboxPath.First() != '/') {
                dropboxPath = $"/{dropboxPath}";
            }

            // remove slash in the end		
            if (dropboxPath.Last() == '/') {
                dropboxPath = dropboxPath.Substring(1, dropboxPath.Length - 1);
            }

            // API: 'Specify the root folder as an empty string rather than as "/"'
            if (dropboxPath == "/") {
                dropboxPath = "";
            }

            return dropboxPath;
        }

        public static string GetMetadataLocalFilePath(string dropboxPath, DropboxSyncConfiguration config) {
            dropboxPath = UnifyDropboxPath(dropboxPath);
            return DropboxPathToLocalPath(dropboxPath, config) + DropboxSyncConfiguration.METADATA_EXTENSION;
        }

        public static string DropboxPathToLocalPath(string dropboxPath, DropboxSyncConfiguration config) {
            string relativeDropboxPath = UnifyDropboxPath(dropboxPath);

            if (relativeDropboxPath.First() == '/') {
                relativeDropboxPath = relativeDropboxPath.Substring(1);
            }

            var fullPath = Path.Combine(config.cacheDirecoryPath, relativeDropboxPath);
            // replace slashes with backslashes if needed
            fullPath = Path.GetFullPath(fullPath);

            return fullPath;
        }

        public static string GetDownloadTempFilePath(string targetLocalPath, string content_hash) {
            string piece_of_hash = content_hash.Substring(0, 10);
            return $"{targetLocalPath}.{piece_of_hash}{DropboxSyncConfiguration.INTERMEDIATE_DOWNLOAD_FILE_EXTENSION}";
        }
        public static bool IsAppKeyValid(string appKey) {
            return appKey != null && appKey.Trim().Length > 0;
        }

        public static bool IsAppSecretValid(string appSecret) {
            return appSecret != null && appSecret.Trim().Length > 0;
        }

        public static bool IsAccessTokenValid(string accessToken) {
            return accessToken != null && accessToken.Trim().Length > 0;
        }

        public static string FixDropboxJSONString(string jsonStr) {

            jsonStr = jsonStr.Replace("\".tag\"", "\"tag\"");

            return jsonStr;
        }

        public static T GetDropboxResponseFromJSON<T>(string jsonStr) {
            jsonStr = FixDropboxJSONString(jsonStr);
            return UnityEngine.JsonUtility.FromJson<T>(jsonStr);
        }

        public static void EnsurePathFoldersExist(string path) {
            var dirPath = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dirPath);
        }

        public static IEnumerable<long> LongRange(long start, long count) {
            var limit = start + count;

            while (start < limit) {
                yield return start;
                start++;
            }
        }

    }

}