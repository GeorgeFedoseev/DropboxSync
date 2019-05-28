using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBXSync;
using UnityEngine;

public class DownloadFileExampleScript : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {

        var downloadTasks = new List<Task<string>>();
        for(var i = 0; i < 5; i++){
            downloadTasks.Add(DropboxSync.Main.CacheManager.GetLocalFilePathAsync("/DropboxSyncExampleFolder/video.mp4", 
                    new Progress<int>((progress) => {
                        //Debug.Log($"Downloading: {progress}%");
                    })));
        }

        var results = await Task.WhenAll(downloadTasks);
        print("All file downloads completed");       
        
        //Debug.Log($"Download finished; file metadata: {metadata}");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
