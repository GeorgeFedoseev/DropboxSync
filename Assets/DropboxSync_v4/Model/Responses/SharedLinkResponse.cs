using UnityEngine;

namespace DBXSync
{
    [System.Serializable]
    public class SharedLinkResponse : Response
    {
        public string tag;
        public string url;
        public string id;
        public string name;
        public string path_lower;
        public LinkPermissions link_permissions;
        public string preview_type;
        public string client_modified;
        public string server_modified;
        public string rev;
        public int size;

        public SharedLinkMetadata GetMetadata()
        {
            return JsonUtility.FromJson<SharedLinkMetadata>(this.ToString());
        }
    }
}
