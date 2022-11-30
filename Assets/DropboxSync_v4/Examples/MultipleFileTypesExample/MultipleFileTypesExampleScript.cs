/// DropboxSync v2.1.1
// Created by George Fedoseev 2018-2019

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DBXSync;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Linq;

public class MultipleFileTypesExampleScript : MonoBehaviour {

	private static readonly string EXAMPLE_TXT_PATH = "/DropboxSyncExampleFolder/earth.txt";
	private static readonly string EXAMPLE_JSON_PATH = "/DropboxSyncExampleFolder/object.json";
	private static readonly string EXAMPLE_IMAGE_PATH = "/DropboxSyncExampleFolder/image.jpg";
	private static readonly string EXAMPLE_VIDEO_PATH = "/DropboxSyncExampleFolder/video.mp4";

	[System.Serializable]
	public class PlanetInfo {
		public string name;
		public float radius_km;
		public List<string> moons;
	}


	public Text planetDescriptionText;

	public Text planetInfoText;

	public RawImage rawImage;

	public VideoPlayer videoPlayer;
	RenderTexture videoRenderTexture = null;

	// Use this for initialization
	void Start () {

		// TEXT
		DropboxSync.Main.GetFile<string>(EXAMPLE_TXT_PATH, new Progress<TransferProgressReport>((progress) => {}),
		(text) => {			
			Debug.Log("Received text string from Dropbox!");			
			UpdatePlanetDescription(text);			
		}, (ex) => {
			Debug.LogError($"Error getting text string: {ex}");
		}, receiveUpdates:true, useCachedFirst: true);

		// JSON OBJECT
		DropboxSync.Main.GetFile<PlanetInfo>(EXAMPLE_JSON_PATH, new Progress<TransferProgressReport>((progress) => {}), 
		(planetInfo) => {
			Debug.Log("Received JSON object from Dropbox!");			
			UpdatePlanetInfo(planetInfo);
		}, (ex) => {
			Debug.LogError($"Error getting JSON object: {ex}");
		}, receiveUpdates:true, useCachedFirst: true);

		
		// IMAGE
		DropboxSync.Main.GetFile<Texture2D>(EXAMPLE_IMAGE_PATH, new Progress<TransferProgressReport>((progress) => {}), 
		(tex) => {			
			UpdatePicture(tex);			
		}, (ex) => {
			Debug.LogError($"Error getting picture from Dropbox: {ex}");
		}, receiveUpdates: true, useCachedFirst:true);


		// VIDEO
		DropboxSync.Main.GetFileAsLocalCachedPath(EXAMPLE_VIDEO_PATH, new Progress<TransferProgressReport>((progress) => {}),
		(localPath) => {
			UpdateVideo(localPath);
		}, (ex) => {
			Debug.LogError($"Error getting video from Dropbox: {ex}");
		}, receiveUpdates:true, useCachedFirst: true);		
	}


	// UI-update methods

	void UpdatePlanetDescription(string desc){
		planetDescriptionText.text = desc;
	}

	void UpdatePlanetInfo(PlanetInfo planet){
		planetInfoText.text = $"Name: {planet.name}\nRadius: {planet.radius_km}km\nMoons: {string.Join(", ", planet.moons)}";
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
