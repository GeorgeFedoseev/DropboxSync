using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectChangesExampleScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() {
        DropboxSync.Main.ChangesManager.SubscribeToFolder("/DropboxSyncExampleFolder/learning_c", (change) => {
            print($"Change detected: {change}");
        });

        DropboxSync.Main.ChangesManager.SubscribeToFolder("/DropboxSyncExampleFolder", (change) => {
            print($"Change detected: {change}");
        });
    }

    
}
