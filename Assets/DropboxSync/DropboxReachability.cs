// using System;
// using System.Net;
// using System.Threading;

// using UnityEngine;
// #if UNITY_EDITOR
// using UnityEditor;
// #endif

// namespace DBXSync {

//     public class DropboxReachability : IDisposable {

//         private static DropboxReachability _instance;
//         public static DropboxReachability Main {
//             get {
//                 if (_instance == null) {
//                     _instance = new DropboxReachability ();
//                 }
//                 return _instance;
//             }            
//         }

//         private static bool _isOnline = false;
//         public static bool IsOnline {
//             get {
//                 return _isOnline;
//             }
//         }

//         private bool _isInitialized = false;
//         private Thread _backgroundThread;
//         private volatile bool _isDisposed = false;

//         private int _pingIntervalMilliseconds;

//         public void Initialize (int pingIntervalMilliseconds) {
//             if (_isInitialized) {
//                 return;
//             }

//             _pingIntervalMilliseconds = pingIntervalMilliseconds;

//             // create thread and start monitoring
//             _backgroundThread = new Thread (_backgroundWorker);
//             _backgroundThread.IsBackground = true;
//             _backgroundThread.Start ();

//             _isInitialized = false;

// #if UNITY_EDITOR
//             EditorApplication.playModeStateChanged += (state) => {
//                 if(state == PlayModeStateChange.ExitingPlayMode) {
//                     this.Dispose();
//                 }
//             };
// #endif

//         }

//         public void SetPingInterval(int pingIntervalMilliseconds){
//             _pingIntervalMilliseconds = pingIntervalMilliseconds;
//         }

//         private void _backgroundWorker () {
//             while (!_isDisposed) {
//                 _isOnline = OpenReadTest();
//                 if(!_isOnline){
//                     Debug.Log("Failed to oppen read");
//                 }
//                 Thread.Sleep(_pingIntervalMilliseconds);
//             }
//         }

//         public static bool OpenReadTest(){
//             try {   
//                 using (WebClientWithTimeout client = new WebClientWithTimeout()){
//                     client.Timeout = 10000;
//                     client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);                    
//                     using (client.OpenRead("http://dropbox.com/")){
//                         return true;
//                     }
//                 }
//             }catch(Exception ex){
//                 Debug.LogException(ex);
//                 return false;
//             }
//         }


//         public void Dispose () {
//             Debug.Log("DropboxReachability.Dispose()");
//             _isDisposed = true;
//             _instance = null;
//         }
//     }

// }