using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DBXSync {
    public class TransferRowScript : MonoBehaviour {

        [SerializeField]
        private Text nameText, pathText, statusText;

        [SerializeField]
        private Image progressBar;

        [SerializeField]
        private Button actionButton;


        private IFileTransfer _transfer;

        public void InitWith(IFileTransfer transfer, TransferManagerWidgetScript.TransferRowType rowType) {
            _transfer = transfer;

            nameText.text = Path.GetFileName(transfer.DropboxPath);
            pathText.text = transfer.DropboxPath;

            progressBar.fillAmount = 0;

            switch (rowType) {
                case TransferManagerWidgetScript.TransferRowType.Active:
                    statusText.text = $"{(_transfer is DownloadFileTransfer ? "Downloading" : "Uploading")}";
                    transfer.ProgressCallback.ProgressChanged += OnProgress;
                    actionButton.GetComponentInChildren<Text>().text = "Cancel";
                    actionButton.onClick.AddListener(_transfer.Cancel);
                    break;
                case TransferManagerWidgetScript.TransferRowType.Queued:
                    statusText.text = "Queued";
                    actionButton.GetComponentInChildren<Text>().text = "Cancel";
                    actionButton.onClick.AddListener(_transfer.Cancel);
                    break;
                case TransferManagerWidgetScript.TransferRowType.Failed:
                    statusText.text = "Failed";
                    progressBar.color = Color.red;
                    actionButton.gameObject.SetActive(false);
                    break;
                case TransferManagerWidgetScript.TransferRowType.Completed:
                    statusText.text = "Completed";
                    actionButton.gameObject.SetActive(false);
                    break;
            }
        }

        void OnProgress(object sender, TransferProgressReport report) {
            statusText.text = $"{(_transfer is DownloadFileTransfer ? "Downloading" : "Uploading")} {report.progress}% {report.bytesPerSecondFormatted}";
            progressBar.fillAmount = (float)report.progress / 100;
        }

        void OnDestroy() {
            if (_transfer != null) {
                _transfer.ProgressCallback.ProgressChanged -= OnProgress;
            }
        }

    }
}


