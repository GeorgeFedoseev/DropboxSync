// DropboxSync v1.1
// Created by George Fedoseev 2018

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

using DBXSync.Model;
using DBXSync.Utils;
using UnityEngine.UI;
using System.IO;
using System.Threading;

namespace DBXSync {
	public partial class DropboxSync: MonoBehaviour {

		// BASE REQUESTS

		void MakeDropboxRequest<T>(string url, T parametersObject, 
				Action<string> onResponse, Action<float> onProgress, Action<DBXError> onWebError) where T : DropboxRequestParams{
			MakeDropboxRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}	

		void MakeDropboxRequest(string url, string jsonParameters, 
				Action<string> onResponse, Action<float> onProgress, Action<DBXError> onWebError){
			Log("MakeDropboxRequest url: "+url);

			DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);
		

			DropboxSyncUtils.IsOnlineAsync((isOnline) => {
				if(!isOnline){
					onWebError(new DBXError("No internet connection", DBXErrorType.NetworkProblem));
					return;
				}

				try {
					using (var client = new DBXWebClient()){				
						client.Headers.Set("Authorization", "Bearer "+DropboxAccessToken);
						client.Headers.Set("Content-Type", "application/json");
						
						client.DownloadProgressChanged += (s, e) => {
							if(onProgress != null){
								Log(string.Format("Downloaded {0} bytes out of {1}", e.BytesReceived, e.TotalBytesToReceive));
								if(e.TotalBytesToReceive != -1){
									// if download size in known from server
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
									});
								}else{
									// return progress is going but unknown
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onProgress(-1);
									});
								}
							}						
						};

						client.UploadDataCompleted += (s, e) => {
							Log("MakeDropboxRequest -> UploadDataCompleted");	
							_activeWebClientsList.Remove(client);					

							if(e.Error != null){
								//LogError("MakeDropboxRequest -> UploadDataCompleted -> Error "+e.Error.Message);
								
								if(e.Error is WebException){
									
									var webex = e.Error as WebException;

									try{
										var stream = webex.Response.GetResponseStream();
										var reader = new StreamReader(stream);
										var responseStr = reader.ReadToEnd();
																									
										var dict = JSON.FromJson<Dictionary<string, object>>(responseStr);
										var errorSummary = dict["error_summary"].ToString();								
										
										_mainThreadQueueRunner.QueueOnMainThread(() => {
											onWebError(new DBXError(errorSummary, DBXError.DropboxAPIErrorSummaryToErrorType(errorSummary)));
										});
									}catch{
										_mainThreadQueueRunner.QueueOnMainThread(() => {
											onWebError(new DBXError(e.Error.Message, DBXErrorType.ParsingError));
										});
									}
								}else{
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onWebError(new DBXError(e.Error.Message, DBXErrorType.Unknown));
									});
								}

							}else{
								// no error
								var respStr = Encoding.UTF8.GetString(e.Result);							
								_mainThreadQueueRunner.QueueOnMainThread(() => {								
									onResponse(respStr);
								});
							}
						};

						var uri = new Uri(url);
						Log("MakeDropboxRequest:client.UploadDataAsync");				
						client.UploadDataAsync(uri, "POST", Encoding.Default.GetBytes(jsonParameters));	
						_activeWebClientsList.Add(client);					
					}
				} catch (Exception ex){
					//onWebError(ex.Message);
					//Log("caught exeption");
					onWebError(new DBXError(ex.Message, DBXErrorType.Unknown));
					//Log(ex.Response.ToString());
				}
			});
		}

		


		// DOWNLOAD BYTES
		void MakeDropboxDownloadRequest<T>(string url, T parametersObject, 
				Action<DBXFile, byte[]> onResponse, Action<float> onProgress, Action<DBXError> onWebError) where T : DropboxRequestParams{
			MakeDropboxDownloadRequest(url, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}

		void MakeDropboxDownloadRequest(string url, string jsonParameters, 
							Action<DBXFile, byte[]> onResponse, Action<float> onProgress, Action<DBXError> onWebError){
			DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);

			DropboxSyncUtils.IsOnlineAsync((isOnline) => {
				if(!isOnline){
					onWebError(new DBXError("No internet connection", DBXErrorType.NetworkProblem));
					return;
				}

				try {
					using (var client = new DBXWebClient()){				
						client.Headers.Set("Authorization", "Bearer "+DropboxAccessToken);					
						client.Headers.Set("Dropbox-API-Arg", jsonParameters);
						
						client.DownloadProgressChanged += (s, e) => {
							
							if(onProgress != null){
								//Log(string.Format("Downloaded {0} bytes out of {1} ({2}%)", e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage));
								if(e.TotalBytesToReceive != -1){
									// if download size in known from server
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onProgress((float)e.BytesReceived/e.TotalBytesToReceive);	
									});
								}else{
									// return progress is going but unknown
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onProgress(-1);
									});
								}
							}						
						};

						
						client.DownloadDataCompleted += (s, e) => {
							_activeWebClientsList.Remove(client);

							if(e.Error != null){
								if(e.Error is WebException){
									var webex = e.Error as WebException;
									var stream = webex.Response.GetResponseStream();
									var reader = new StreamReader(stream);
									var responseStr = reader.ReadToEnd();
									Log(responseStr);

									try{								
										var dict = JSON.FromJson<Dictionary<string, object>>(responseStr);
										var errorSummary = dict["error_summary"].ToString();	
										_mainThreadQueueRunner.QueueOnMainThread(() => {							
											onWebError(new DBXError(errorSummary, DBXError.DropboxAPIErrorSummaryToErrorType(errorSummary)));
										});
									}catch{
										_mainThreadQueueRunner.QueueOnMainThread(() => {
											onWebError(new DBXError(e.Error.Message, DBXErrorType.ParsingError));
										});
									}
								}else{
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										onWebError(new DBXError(e.Error.Message, DBXErrorType.Unknown));
									});
								}
							}else if(e.Cancelled){
								_mainThreadQueueRunner.QueueOnMainThread(() => {
									onWebError(new DBXError("Download was cancelled.", DBXErrorType.UserCanceled));
								});
							}else{
								//var respStr = Encoding.UTF8.GetString(e.Result);
								var metadataJsonStr = client.ResponseHeaders["Dropbox-API-Result"].ToString();
								Log(metadataJsonStr);
								var dict = JSON.FromJson<Dictionary<string, object>>(metadataJsonStr);
								var fileMetadata = DBXFile.FromDropboxDictionary(dict);

								_mainThreadQueueRunner.QueueOnMainThread(() => {
									onResponse(fileMetadata, e.Result);
								});							
							}
						};

						var uri = new Uri(url);
						client.DownloadDataAsync(uri);
						_activeWebClientsList.Add(client);
					}
				} catch (WebException ex){
					_mainThreadQueueRunner.QueueOnMainThread(() => {
						onWebError(new DBXError(ex.Message, DBXErrorType.Unknown));
					});
				}
			});
		}

		// UPLOAD BYTES
		void MakeDropboxUploadRequest<T>(string url, byte[] dataToUpload, T parametersObject,
								 Action<DBXFile> onResponse, Action<float> onProgress, Action<DBXError> onWebError){
			MakeDropboxUploadRequest(url, dataToUpload, JsonUtility.ToJson(parametersObject), onResponse, onProgress, onWebError);
		}

		void MakeDropboxUploadRequest(string url, byte[] dataToUpload, string jsonParameters,
												 Action<DBXFile> onResponse, Action<float> onProgress, Action<DBXError> onWebError){
			DropboxSyncUtils.ValidateAccessToken(DropboxAccessToken);


			DropboxSyncUtils.IsOnlineAsync((isOnline) => {
				if(!isOnline){
					onWebError(new DBXError("No internet connection", DBXErrorType.NetworkProblem));
					return;
				}

				try {
					using (var client = new DBXWebClient()){			
						

						client.Headers.Set("Authorization", "Bearer "+DropboxAccessToken);					
						client.Headers.Set("Dropbox-API-Arg", jsonParameters);
						client.Headers.Set("Content-Type", "application/octet-stream");

						
						
						client.UploadProgressChanged += (s, e) => {
							Log(string.Format("Upload {0} bytes out of {1} ({2}%)", e.BytesSent, e.TotalBytesToSend, e.ProgressPercentage));

							if(onProgress != null){
								_mainThreadQueueRunner.QueueOnMainThread(() => {
									if(e.ProgressPercentage == 50){
										// waiting for Dropbox to reply
										onProgress(0.99f);
									}else{
										onProgress((float)e.BytesSent/e.TotalBytesToSend);
									}
										
								});							
							}						
						};

						client.UploadDataCompleted += (s, e) => {
							Log("MakeDropboxUploadRequest -> UploadDataCompleted");
							_activeWebClientsList.Remove(client);

							if(e.Error != null){
								if(e.Error is WebException){
									var webex = e.Error as WebException;
									var stream = webex.Response.GetResponseStream();
									var reader = new StreamReader(stream);
									var responseStr = reader.ReadToEnd();
									LogWarning(responseStr);

									try{								
										var dict = JSON.FromJson<Dictionary<string, object>>(responseStr);
										var errorSummary = dict["error_summary"].ToString();	
										_mainThreadQueueRunner.QueueOnMainThread(() => {							
											onWebError(new DBXError(errorSummary, DBXErrorType.DropboxAPIError));
										});
									}catch{
										_mainThreadQueueRunner.QueueOnMainThread(() => {
											onWebError(new DBXError(e.Error.Message, DBXErrorType.ParsingError));
										});
									}
								}else{
									_mainThreadQueueRunner.QueueOnMainThread(() => {
										Log("e.Error is "+e.Error);
										onWebError(new DBXError(e.Error.Message, DBXErrorType.Unknown));
									});
								}
							}else if(e.Cancelled){
								Log("MakeDropboxUploadRequest -> canceled");
								_mainThreadQueueRunner.QueueOnMainThread(() => {
									onWebError(new DBXError("Download was cancelled.", DBXErrorType.UserCanceled));
								});
							}else{
								Log("MakeDropboxUploadRequest -> no error");
								//var respStr = Encoding.UTF8.GetString(e.Result);
								var metadataJsonStr = Encoding.UTF8.GetString(e.Result);;
								Log(metadataJsonStr);
								var dict = JSON.FromJson<Dictionary<string, object>>(metadataJsonStr);
								var fileMetadata = DBXFile.FromDropboxDictionary(dict);

								_mainThreadQueueRunner.QueueOnMainThread(() => {
									onResponse(fileMetadata);
								});							
							}
						};

						var uri = new Uri(url);

						// don't use UploadFile (https://stackoverflow.com/questions/18539807/how-to-remove-multipart-form-databoundary-from-webclient-uploadfile)
						client.UploadDataAsync(uri, "POST", dataToUpload);
						_activeWebClientsList.Add(client);
					}
				} catch (WebException ex){
					_mainThreadQueueRunner.QueueOnMainThread(() => {
						onWebError(new DBXError(ex.Message, DBXErrorType.Unknown));
					});
				}
			});

			
		}

		
	}
}
