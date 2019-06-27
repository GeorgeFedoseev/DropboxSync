namespace DBXSync {

    [System.Serializable]
    public class CreateFolderRequestParameters : RequestParameters {
        public string path;
        public bool autorename = false;
    }
}