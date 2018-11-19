using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using UnityEngine.Video;
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
				var textStr = res.data;
				UpdatePlanetDescription(textStr);
			}
		}, receiveUpdates:true);

		// JSON OBJECT
		DropboxSync.Main.GetFile<JsonObject>("/DropboxSyncExampleFolder/object.json", (res) => {
			if(res.error){
				Debug.LogError("Error getting JSON object: "+res.errorDescription);
			}else{
				Debug.Log("Received JSON object from Dropbox!");
				var jsonObject = res.data;
				UpdatePlanetInfo(jsonObject);
			}
		}, receiveUpdates:true);

		
		// IMAGE
		DropboxSync.Main.GetFile<Texture2D>("/DropboxSyncExampleFolder/image.jpg", (res) => {
			if(res.error){
				Debug.LogError("Error getting picture from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received picture from Dropbox!");
				var tex = res.data;
				UpdatePicture(tex);
			}
		}, useCachedFirst:true);


		// VIDEO
		DropboxSync.Main.GetFileAsLocalCachedPath("/DropboxSyncExampleFolder/video.mp4", (res) => {
			if(res.error){
				Debug.LogError("Error getting video from Dropbox: "+res.errorDescription);
			}else{
				Debug.Log("Received video from Dropbox!");
				var filePathInCache = res.data;
				UpdateVideo(filePathInCache);
			}
		}, receiveUpdates:true);


		// BYTES ARRAY
		DropboxSync.Main.GetFileAsBytes("/DropboxSyncExampleFolder/image.jpg", (res) => {
			if(res.error){
				Debug.LogError("Failed to get file bytes: "+res.errorDescription);
			}else{
				var imageBytes = res.data;
				Debug.Log("Got file as bytes array, length: "+imageBytes.Length.ToString()+" bytes");
			}
		}, receiveUpdates:true);		
		
	}


	// UI-update methods

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
		
		videoPlayer.source = VideoSource.Url;
		videoPlayer.url = localVideoPath;
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
