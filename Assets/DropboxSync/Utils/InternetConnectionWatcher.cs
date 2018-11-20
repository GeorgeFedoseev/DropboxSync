using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace DBXSync.Utils {

	public class InternetConnectionWatcher {
		float INTERNET_CONNECTION_CHECK_INTERVAL_SECONDS = 5f;
		
		public Action OnLostInternetConnection = () => {};

		bool _isConnected = false;
		public bool IsConnected {
			get {
				return _isConnected;
			}
		}

		private List<Action> _onInternetRecoverOnceCallbacks = new List<Action>();

		float _lastTimeCheckedInternetConnection = -999;

		public void Update(){
			if(Time.unscaledTime - _lastTimeCheckedInternetConnection > INTERNET_CONNECTION_CHECK_INTERVAL_SECONDS){
				DropboxSyncUtils.IsOnlineAsync((isOnline) => {
					if(isOnline){
						if(!_isConnected){					

							foreach(var a in _onInternetRecoverOnceCallbacks){
								a();
							}
							_onInternetRecoverOnceCallbacks.Clear();
						}

						_isConnected = true;					
					}else{
						if(_isConnected){
							OnLostInternetConnection();
						}	

						_isConnected = false;
					}
				});
				_lastTimeCheckedInternetConnection = Time.unscaledTime;
			}
			
		}

		// METHODS

		public void SubscribeToInternetConnectionRecoverOnce(Action a){
			_onInternetRecoverOnceCallbacks.Add(a);

		}
		
	}
}