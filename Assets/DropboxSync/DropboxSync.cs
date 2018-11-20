// DropboxSync v1.1
// Created by George Fedoseev 2018

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using UnityEngine;

using DBXSync.Model;
using DBXSync.Utils;
using UnityEngine.UI;
using System.IO;
using System.Threading;

namespace DBXSync {

	public partial class DropboxSync : MonoBehaviour {
		

		// SINGLETONE
		public static DropboxSync Main {
			get {
				var instance = FindObjectOfType<DropboxSync>();
				if(instance != null){
					return instance;
				}else{
					Debug.LogError("DropboxSync script wasn't found on the scene.");
					return null;
				}
			}
		}

		// <INSPECTOR

		[HideInInspector]
		public float DBXCheckForChangesIntervalSeconds = 15;
		public string DropboxAccessToken = "<YOUR ACCESS TOKEN>";

		// INSPECTOR>		

		// INTERNET CONNECTION
		InternetConnectionWatcher _internetConnectionWatcher;
		
		// TIMERS
		float _lastTimeCheckedForSubscribedItemsChanges = -999999;

		// MAIN THREAD
		List<Action> MainThreadQueuedActions = new List<Action>();

		// OTHER
		string _PersistentDataPath = null;

		// MONOBEHAVIOUR
		void Awake(){
			Initialize();
		}

		void Update () {
			_internetConnectionWatcher.Update();
			
			// check remote changes for subscribed
			if(Time.unscaledTime - _lastTimeCheckedForSubscribedItemsChanges > DBXCheckForChangesIntervalSeconds){									
				if(_internetConnectionWatcher.IsConnected){
					CheckChangesForSubscribedItems();
				}				
				_lastTimeCheckedForSubscribedItemsChanges = Time.unscaledTime;
			}

			// execute main thread queued actions
			lock(MainThreadQueuedActions){				
				foreach(var a in MainThreadQueuedActions){
					if(a != null){
						a();
					}						
				}

				MainThreadQueuedActions.Clear();
			}
		}

		// METHODS

		void Initialize(){
			_PersistentDataPath = Application.persistentDataPath;	

			_internetConnectionWatcher = new InternetConnectionWatcher();

			// trust all certificates
			// TODO: do something smarter instead of this
			System.Net.ServicePointManager.ServerCertificateValidationCallback =
    							((sender, certificate, chain, sslPolicyErrors) => true);		
		}

		// THREADING

		void QueueOnMainThread(Action a){
			lock(MainThreadQueuedActions){
				MainThreadQueuedActions.Add(a);
			}
		}
	} // class
} // namespace
