using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChangesSubscriptionExampleScript : MonoBehaviour
{

    public Button subscribeButton, unsubscribeButton;
    // Start is called before the first frame update
    void Start() {
        var path = "/DropboxSyncExampleFolder/learning_c/file038.bin";

        subscribeButton.onClick.AddListener(() => {
            DropboxSync.Main.ChangesManager.SubscribeToChanges(path, OnChangeInFolder);        
        });

        unsubscribeButton.onClick.AddListener(() => {
            DropboxSync.Main.ChangesManager.UnsubscribeFromChanges(path, OnChangeInFolder);        
        });       
    }


    void OnChangeInFolder(DBXSync.EntryChange change){
        print($"Change detected: {change}");
    }    
}
