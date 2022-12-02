namespace DBXSync
{
    [System.Serializable]
    public class Audience
    {
        public string tag;
    }

    [System.Serializable]
    public class AudienceOption : JSONSerializableObject
    {
        public Audience audience;
        public bool allowed;
        public DisallowedReason disallowed_reason;
    }
}
