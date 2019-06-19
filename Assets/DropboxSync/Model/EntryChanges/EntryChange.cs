namespace DBXSync {

    [System.Serializable]
    public class EntryChange : JSONSerializableObject {
        public string dropboxPath;
        public EntryChangeType type;
        public Metadata latestMetadata;        
    }

}