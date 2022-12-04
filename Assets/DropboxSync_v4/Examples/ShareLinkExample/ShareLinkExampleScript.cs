using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DBXSync;
using UnityEngine;
using UnityEngine.UI;

public class ShareLinkExampleScript : MonoBehaviour {

    public InputField inputField, sharedLinkField;
    public Button shareButton, openLinkButton;
    public Text statusText;


    // Start is called before the first frame update
    void Start() {
        shareButton.onClick.AddListener(ShareDropboxPath);
        openLinkButton.onClick.AddListener(OpenSharedLinkInBrowser);
    }

    private void ShareDropboxPath() {

        DropboxSync.Main.CreateSharedLinkWithSettings(
            inputField.text,
            (md) => {
                statusText.text = $"<color=green>Successfully created share link.</color>";
                sharedLinkField.text = md.url;
            }, (ex) => {
                statusText.text = $"<color=red>Failed to create share link {ex}</color>";
                sharedLinkField.text = "";
                Debug.LogError($"Failed to create share link: {ex}");
            }, audience: LinkAudienceParam.PUBLIC, access: RequestedLinkAccessLevelParam.EDITOR, allow_download: false
        );
    }

    private void OpenSharedLinkInBrowser(){
        Application.OpenURL(sharedLinkField.text);
    }
}
