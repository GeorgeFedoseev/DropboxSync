using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CheckUpdatesExampleScript : MonoBehaviour {

    private static string FILE_PATH_ON_DROPBOX = "/DropboxSyncExampleFolder/video.mp4";

    [SerializeField]
    private Button _checkButton, _downloadButton, _cancelButton;

    [SerializeField]
    private Text _statusText;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    // Start is called before the first frame update
    void Start() {
        _checkButton.onClick.AddListener(CheckUpdates);
        _downloadButton.onClick.AddListener(DownloadUpdate);
        _cancelButton.onClick.AddListener(() => {
            _cancellationTokenSource.Cancel();
        });

        _checkButton.gameObject.SetActive(true);
        _downloadButton.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(false);
    }

    private void CheckUpdates() {

        _checkButton.gameObject.SetActive(false);
        _downloadButton.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(false);

        _statusText.text = "Checking for updates...";

        DropboxSync.Main.ShouldUpdateFileFromDropbox(FILE_PATH_ON_DROPBOX,
        (needUpdate) => {
            if (needUpdate) {
                _statusText.text = "Update available.";
                _downloadButton.gameObject.SetActive(true);
            } else {
                _statusText.text = "File is up to date.";
                _checkButton.gameObject.SetActive(true);
            }
        }, (ex) => {
            _statusText.text = $"Failed to check the update: {ex}";
            _checkButton.gameObject.SetActive(true);
        });
    }

    private void DownloadUpdate() {
        _cancellationTokenSource = new CancellationTokenSource();

        _checkButton.gameObject.SetActive(false);
        _downloadButton.gameObject.SetActive(false);
        _cancelButton.gameObject.SetActive(true);

        _statusText.text = "Downloading update...";
        DropboxSync.Main.GetFileAsLocalCachedPath(FILE_PATH_ON_DROPBOX,
         new System.Progress<DBXSync.TransferProgressReport>((report) => {
             _statusText.text = $"Downloading update {report.progress}% {report.bytesPerSecondFormatted}";
         }),
         (localPath) => {
             _statusText.text = $"Update downloaded.";
             _checkButton.gameObject.SetActive(true);
             _downloadButton.gameObject.SetActive(false);
             _cancelButton.gameObject.SetActive(false);
         }, (ex) => {
             _statusText.text = $"Failed to download update: {ex}";
             _checkButton.gameObject.SetActive(true);
             _downloadButton.gameObject.SetActive(false);
             _cancelButton.gameObject.SetActive(false);
         }, useCachedIfOffline: false, cancellationToken: _cancellationTokenSource.Token);

    }


}
