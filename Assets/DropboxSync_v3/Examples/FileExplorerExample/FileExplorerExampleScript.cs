using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FileExplorerExampleScript : MonoBehaviour {
    // Start is called before the first frame update
    async void Start() {

        // create folder test
        var folderPath = "/nested/folder/path";
        var folderMetadata = await DropboxSync.Main.CreateFolderAsync(folderPath, autorename: true);
        Debug.Log($"Created folder: {folderMetadata}");

        // move dat folder
        var moveToFolderPath = "/moved/nested/folder/path";
        await DropboxSync.Main.MoveAsync(folderPath, moveToFolderPath, autorename: true);

        // delete test
        await DropboxSync.Main.DeleteAsync(moveToFolderPath);

        Debug.LogWarning("All done");


    }
    
}
