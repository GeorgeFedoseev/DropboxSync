using System.Threading;
using UnityEngine;

namespace DBXSync {
    public static class SyncContextUtil {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() {
            UnitySynchronizationContext = SynchronizationContext.Current;
            UnityThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static int UnityThreadId {
            get;
            private set;
        }

        public static SynchronizationContext UnitySynchronizationContext {
            get;
            private set;
        }
    }
}