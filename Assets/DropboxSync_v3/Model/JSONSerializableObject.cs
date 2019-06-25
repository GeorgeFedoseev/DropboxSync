namespace DBXSync {

    public class JSONSerializableObject {

        public override string ToString() {
            return UnityEngine.JsonUtility.ToJson(this);
        }
    }
}