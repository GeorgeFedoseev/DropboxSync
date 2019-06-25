namespace DBXSync {

    [System.Serializable]
    public class EntryChange : JSONSerializableObject {        
        public EntryChangeType type;
        public Metadata metadata;

        public override string ToString(){
            return $"[{type}]: {metadata}";
        }
    }

}