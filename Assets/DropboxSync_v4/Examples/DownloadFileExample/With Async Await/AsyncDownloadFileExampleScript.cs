using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DBXSync;
using UnityEngine;
using UnityEngine.UI;

public class AsyncDownloadFileExampleScript : MonoBehaviour {

    public InputField inputField;
    public Button downloadButton, cancelButton;
    public Text statusText;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    // Start is called before the first frame update
    void Start() {
        downloadButton.onClick.AddListener(DownloadFile);

        cancelButton.onClick.AddListener(() => {
            _cancellationTokenSource.Cancel();
        });
    }

    private async void DownloadFile() {
        _cancellationTokenSource = new CancellationTokenSource();

        try {
            var localPath = await DropboxSync.Main.GetFileAsLocalCachedPathAsync(inputField.text,
                                new Progress<TransferProgressReport>((report) => {
                                    statusText.text = $"Downloading: {report.progress}% {report.bytesPerSecondFormatted}";
                                }), _cancellationTokenSource.Token);

            print($"Completed");
            statusText.text = $"<color=green>Local path: {localPath}</color>";

        } catch (Exception ex) {
            if (ex is OperationCanceledException) {
                Debug.Log("Download cancelled");
                statusText.text = $"<color=orange>Download canceled.</color>";
            } else {
                Debug.LogException(ex);
                statusText.text = $"<color=red>Download failed.</color>";
            }
        }


    }
}
