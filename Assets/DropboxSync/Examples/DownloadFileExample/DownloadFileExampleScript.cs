using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DownloadFileExampleScript : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        var metadata = await DropboxSync.Main.DownloadFileAsync("/DropboxSyncExampleFolder/video.mp4", "/Users/gosha/Desktop/video.mp4", 
                    new Progress<int>((progress) => {
                        Debug.Log($"Downloading: {progress}%");
                    }));
        Debug.Log($"Download finished; file metadata: {metadata}");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
