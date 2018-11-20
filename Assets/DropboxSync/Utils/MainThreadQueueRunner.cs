using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadQueueRunner {

	private object _mainThreadQueuedActionsLock = new object();
	private List<Action> _mainThreadQueuedActions = new List<Action>();
	
	public void PerformQueuedTasks () {
	
		lock(_mainThreadQueuedActionsLock){				
			foreach(var a in _mainThreadQueuedActions){
				if(a != null){
					a();
				}						
			}

			_mainThreadQueuedActions.Clear();
		}
	}

	public void QueueOnMainThread(Action a){
			lock(_mainThreadQueuedActionsLock){
				_mainThreadQueuedActions.Add(a);
			}
		}
}