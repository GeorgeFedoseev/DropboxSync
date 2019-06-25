using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TransferManagerWidgetScript : MonoBehaviour {

    [SerializeField]
    private Text _statusText;

    [SerializeField]
    private DropboxSync _dropboxSyncInstance;


    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        var dropboxSync = _dropboxSyncInstance != null ? _dropboxSyncInstance : DropboxSync.Main;

        if(dropboxSync != null && dropboxSync.TransferManager != null){
            _statusText.text = $"Downloads: {dropboxSync.TransferManager.CurrentDownloadTransferNumber} ({dropboxSync.TransferManager.CurrentQueuedDownloadTransfersNumber} queued)"
                                + $"\nUploads: {dropboxSync.TransferManager.CurrentUploadTransferNumber} ({dropboxSync.TransferManager.CurrentQueuedUploadTransfersNumber} queued)"
                                + $"\nCompleted: {dropboxSync.TransferManager.CompletedTransferNumber}"
                                + $"\nFailed: {dropboxSync.TransferManager.FailedTransfersNumber}"
                                ;
        }

    }
}
