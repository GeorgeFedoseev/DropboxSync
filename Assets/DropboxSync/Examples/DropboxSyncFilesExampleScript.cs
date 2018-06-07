using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

using DBXSync;
using System.Linq;

public class DropboxSyncFilesExampleScript : MonoBehaviour {

	public RawImage rawImage;
	public VideoPlayer videoPlayer;
	RenderTexture videoRenderTexture = null;

	// Use this for initialization
	void Start () {
		
		// IMAGE
		DropboxSync.Main.GetFile<Texture2D>("/DropboxSyncExampleFolder/image.jpg", (res) => {
			if(res.error){
				Debug.LogError("Error getting picture from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received picture from Dropbox!");
				UpdatePicture(res.data);
			}
		}, useCachedIfPossible:true, useCachedIfOffline:true, receiveUpdates:true);


		// VIDEO
		DropboxSync.Main.GetFileAsLocalCachedPath("/DropboxSyncExampleFolder/video.mp4", (res) => {
			if(res.error){
				Debug.LogError("Error getting video from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received video from Dropbox!");
				UpdateVideo(res.data);
			}
		}, useCachedIfPossible:true, useCachedIfOffline:true, receiveUpdates:true);
		
	}

	void UpdatePicture(Texture2D tex){
		rawImage.texture = tex;
		rawImage.GetComponent<AspectRatioFitter>().aspectRatio = (float)tex.width/tex.height;
	}

	void UpdateVideo(string localVideoPath){
		if(localVideoPath == null){
			videoPlayer.Stop();
			videoPlayer.source = VideoSource.VideoClip;
			videoPlayer.GetComponentInChildren<RawImage>().texture = null;		
			return;
		}
		Debug.Log("Update local video path: "+localVideoPath);
		videoPlayer.source = VideoSource.Url;
		videoPlayer.url = "file://"+localVideoPath;

		if(videoRenderTexture == null){
			Debug.Log("Craete render tex");
			videoRenderTexture = new RenderTexture(1024, 728, 16, RenderTextureFormat.ARGB32);
        	videoRenderTexture.Create();
		}
		
		
		videoPlayer.targetTexture = videoRenderTexture;
		videoPlayer.GetComponentInChildren<RawImage>().texture = videoRenderTexture;
		videoPlayer.Play();
		
	}

}
