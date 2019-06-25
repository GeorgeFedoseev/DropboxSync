using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DBXSync;
using UnityEngine;
using UnityEngine.UI;

public class DownloadFileExampleScript : MonoBehaviour
{

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

    private void DownloadFile(){
        _cancellationTokenSource = new CancellationTokenSource();

        DropboxSync.Main.GetFileAsLocalCachedPath(inputField.text, 
                                new Progress<TransferProgressReport>((report) => {                        
                                    statusText.text = $"Downloading: {report.progress}% {report.bytesPerSecondFormatted}";
                                }),
                                (localPath) => {
                                    // success
                                    print($"Completed");
			                        statusText.text = $"<color=green>Local path: {localPath}</color>";
                                },
                                (ex) => {
                                    // exception
                                    if(ex is OperationCanceledException){
                                        Debug.Log("Download cancelled");
                                        statusText.text = $"<color=orange>Download canceled.</color>";
                                    }else{
                                        Debug.LogException(ex);
                                        statusText.text = $"<color=red>Download failed.</color>";
                                    }
                                },
                                _cancellationTokenSource.Token);
        
    }
}
