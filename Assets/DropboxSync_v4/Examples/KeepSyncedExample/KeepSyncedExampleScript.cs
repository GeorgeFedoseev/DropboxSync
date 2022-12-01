using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeepSyncedExampleScript : MonoBehaviour {

    public Button keepSyncedButton, stopSyncingButton;
    public InputField dropboxPath;
    public Text statusText;

    // Start is called before the first frame update
    void Start() {
        keepSyncedButton.onClick.AddListener(() => {
            DropboxSync.Main.KeepSynced(dropboxPath.text, OnChangeSynced);
        });

        stopSyncingButton.onClick.AddListener(() => {
            DropboxSync.Main.StopKeepingInSync(dropboxPath.text);
        });
    }

    void Update() {
        statusText.text = $"Keeping in sync: {DropboxSync.Main.IsKeepingInSync(dropboxPath.text)}";
    }


    void OnChangeSynced(DBXSync.EntryChange change) {
        print($"Change synced: {change}");
        // statusText.text = $"change synced: {change}";
    }
}
