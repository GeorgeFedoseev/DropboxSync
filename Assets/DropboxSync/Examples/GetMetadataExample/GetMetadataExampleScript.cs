using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetMetadataExampleScript : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        var metadata = await DropboxSync.Main.GetFileMetadataAsync("/DropboxSyncExampleFolder/earth.txt");
        print(metadata);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
