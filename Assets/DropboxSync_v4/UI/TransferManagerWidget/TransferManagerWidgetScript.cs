using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DBXSync;
using UnityEngine;
using UnityEngine.UI;

public class TransferManagerWidgetScript : MonoBehaviour {

    public enum TransferRowType {
        Queued,
        Active,
        Failed,
        Completed
    }

    [SerializeField]
    private Text _statusText;

    [SerializeField]
    private GameObject _popUpWindow;

    [SerializeField]
    private Transform _transfersContainer;

    [SerializeField]
    private DropboxSync _dropboxSyncInstance;

    [SerializeField]
    private GameObject _downloadRowPrefab, _uploadRowPrefab;

    [SerializeField]
    private Button _maximizeButton, _minimizeButton;

    [SerializeField]
    private Text _maximizeButtonMiniStatusText;


    private float reloadTransferListAfter = 0;

    // Start is called before the first frame update
    void Start() {
        if (_dropboxSyncInstance == null) {
            _dropboxSyncInstance = DropboxSync.Main;
        }

        if (_dropboxSyncInstance != null && _dropboxSyncInstance.TransferManager != null) {
            // subscribe to transfer list updates
            _dropboxSyncInstance.TransferManager.OnTransfersListChanged += OnTransfersListChanged;
        }

        _maximizeButton.onClick.AddListener(Maximize);
        _minimizeButton.onClick.AddListener(Minimize);

        Minimize();
    }

    // Update is called once per frame
    void Update() {

        if (_dropboxSyncInstance != null && _dropboxSyncInstance.TransferManager != null) {

            if (_popUpWindow.activeInHierarchy) {

                _statusText.text = $"Downloads: {_dropboxSyncInstance.TransferManager.CurrentDownloadTransferNumber} ({_dropboxSyncInstance.TransferManager.CurrentQueuedDownloadTransfersNumber} queued) {_dropboxSyncInstance.TransferManager.CurrentTotalDownloadSpeedFormatted}"
                                    + $"\nUploads: {_dropboxSyncInstance.TransferManager.CurrentUploadTransferNumber} ({_dropboxSyncInstance.TransferManager.CurrentQueuedUploadTransfersNumber} queued) {_dropboxSyncInstance.TransferManager.CurrentTotalUploadSpeedFormatted}"
                                    + $"\nCompleted: {_dropboxSyncInstance.TransferManager.CompletedTransferNumber}"
                                    + $"\nFailed: {_dropboxSyncInstance.TransferManager.FailedTransfersNumber}"
                                    ;


                if (reloadTransferListAfter > 0 && Time.time >= reloadTransferListAfter) {
                    ReloadTransfersList();
                    reloadTransferListAfter = 0;
                }
            } else {
                // mini status
                var totalDownloads = _dropboxSyncInstance.TransferManager.CurrentDownloadTransferNumber + _dropboxSyncInstance.TransferManager.CurrentQueuedDownloadTransfersNumber;
                var totalUploads = _dropboxSyncInstance.TransferManager.CurrentUploadTransferNumber + _dropboxSyncInstance.TransferManager.CurrentQueuedUploadTransfersNumber;

                _maximizeButtonMiniStatusText.text = $"{(totalDownloads > 0 ? $"{totalDownloads} ↓" : "")} {(totalUploads > 0 ? $"{totalUploads} ↑" : "")}";
            }
        }
    }

    // METHODS
    private void Maximize() {
        _popUpWindow.SetActive(true);
        ReloadTransfersList();
    }

    private void Minimize() {
        _popUpWindow.SetActive(false);
    }


    void ReloadTransfersList() {
        ClearContainer(_transfersContainer);

        // order by time finished in each group
        // active
        foreach (var tr in _dropboxSyncInstance.TransferManager.ActiveTransfers.OrderByDescending(x => x.StartDateTime)) {
            InstantiateRowForTransfer(tr, TransferRowType.Active);
        }
        // queued        
        foreach (var tr in _dropboxSyncInstance.TransferManager.QueuedTransfers) {
            InstantiateRowForTransfer(tr, TransferRowType.Queued);
        }
        // failed
        foreach (var tr in _dropboxSyncInstance.TransferManager.FailedTransfers.OrderByDescending(x => x.EndDateTime)) {
            InstantiateRowForTransfer(tr, TransferRowType.Failed);
        }
        // completed
        foreach (var tr in _dropboxSyncInstance.TransferManager.CompletedTransfers.OrderByDescending(x => x.EndDateTime)) {
            InstantiateRowForTransfer(tr, TransferRowType.Completed);
        }

    }

    private void InstantiateRowForTransfer(IFileTransfer transfer, TransferManagerWidgetScript.TransferRowType rowType) {
        var row = InstantiateIntoContainer<TransferRowScript>(transfer is DownloadFileTransfer ? _downloadRowPrefab : _uploadRowPrefab, _transfersContainer);
        row.InitWith(transfer, rowType);
    }

    public static void ClearContainer(Transform container) {
        foreach (Transform t in container) {
            if (Application.isPlaying) {
                GameObject.Destroy(t.gameObject);
            } else {
                GameObject.DestroyImmediate(t.gameObject);
            }
        }
    }

    public static T InstantiateIntoContainer<T>(UnityEngine.Object prefab, Transform container) where T : MonoBehaviour {
        var a = (GameObject.Instantiate(prefab) as GameObject).GetComponent<T>();
        a.transform.SetParent(container);
        a.transform.localScale = Vector3.one;
        a.transform.localPosition = Vector3.zero;

        return a;
    }

    // EVENTS   

    void OnTransfersListChanged() {
        // debounce
        reloadTransferListAfter = Time.time + 0.2f;
    }
}
