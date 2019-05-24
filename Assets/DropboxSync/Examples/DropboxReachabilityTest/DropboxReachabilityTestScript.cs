// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// using DBXSync;

// public class DropboxReachabilityTestScript : MonoBehaviour
// {

//     public Image _indicatorImage;
//     public Text _statusText;
//     public Color _onlineColor, _offlineColor;

//     // Start is called before the first frame update
//     void Start()
//     {
//         DropboxReachability.Main.Initialize(pingIntervalMilliseconds: 1000);
//     }

//     // Update is called once per frame
//     void Update()
//     {
//         _indicatorImage.color =  DropboxReachability.IsOnline ? _onlineColor : _offlineColor;
//         _statusText.text = DropboxReachability.IsOnline ? "Dropbox is reachable" : "Dropbox is not reachable";
        
//     }
// }
