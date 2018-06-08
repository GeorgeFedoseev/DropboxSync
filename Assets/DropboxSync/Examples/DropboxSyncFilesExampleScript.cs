using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

using DBXSync;
using System.Linq;

public class DropboxSyncFilesExampleScript : MonoBehaviour {

	public Text planetDescriptionText;

	public Text planetInfoText;

	public RawImage rawImage;

	public VideoPlayer videoPlayer;
	RenderTexture videoRenderTexture = null;

	// Use this for initialization
	void Start () {

		// TEXT
		DropboxSync.Main.GetFile<string>("/DropboxSyncExampleFolder/earth.txt", (res) => {
			if(res.error){
				Debug.LogError("Error getting text string: "+res.errorDescription);
			}else{
				Debug.Log("Received text string from Dropbox!");
				UpdatePlanetDescription(res.data);
			}
		}, receiveUpdates:true, useCachedFirst:true);

		// JSON OBJECT
		DropboxSync.Main.GetFile<JsonObject>("/DropboxSyncExampleFolder/object.json", (res) => {
			if(res.error){
				Debug.LogError("Error getting JSON object: "+res.errorDescription);
			}else{
				Debug.Log("Received JSON object from Dropbox!");
				UpdatePlanetInfo(res.data);
			}
		}, receiveUpdates:true, useCachedFirst:true);

		
		// IMAGE
		DropboxSync.Main.GetFile<Texture2D>("/DropboxSyncExampleFolder/image.jpg", (res) => {
			if(res.error){
				Debug.LogError("Error getting picture from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received picture from Dropbox!");
				UpdatePicture(res.data);
			}
		}, useCachedFirst:true, useCachedIfOffline:true, receiveUpdates:true);


		// VIDEO
		DropboxSync.Main.GetFileAsLocalCachedPath("/DropboxSyncExampleFolder/video.mp4", (res) => {
			if(res.error){
				Debug.LogError("Error getting video from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received video from Dropbox!");
				UpdateVideo(res.data);
			}
		}, useCachedFirst:true, useCachedIfOffline:true, receiveUpdates:true);

		
		
	}

	void UpdatePlanetDescription(string desc){
		planetDescriptionText.text = desc;
	}

	void UpdatePlanetInfo(JsonObject planet){
		planetInfoText.text = "";
		foreach(var kv in planet){
			var valStr = "";
			if(kv.Value is List<object>){
				valStr = string.Join(", ", ((List<object>)kv.Value).Select(x => x.ToString()).ToArray()); 
			}else{
				valStr = kv.Value.ToString();
			}

			planetInfoText.text += string.Format("<b>{0}:</b> {1}\n", kv.Key, valStr);
		}	
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
		videoPlayer.isLooping = true;

		if(videoRenderTexture == null){			
			videoRenderTexture = new RenderTexture(1024, 728, 16, RenderTextureFormat.ARGB32);
        	videoRenderTexture.Create();
		}		
		
		videoPlayer.targetTexture = videoRenderTexture;
		videoPlayer.GetComponentInChildren<RawImage>().texture = videoRenderTexture;
		videoPlayer.Play();
		
	}

}
