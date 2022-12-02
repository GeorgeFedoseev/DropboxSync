namespace DBXSync
{
    [System.Serializable]
    public class Policy
    {
        public string tag;
    }

    [System.Serializable]
    public class ResolvedPolicy
    {
        public string tag;
    }

    [System.Serializable]
    public class DisallowedReason
    {
        public string tag;
    }

    [System.Serializable]
    public class VisibilityPolicy : JSONSerializableObject
    {
        public Policy policy;
        public ResolvedPolicy resolved_policy;
        public bool allowed;
        public DisallowedReason disallowed_reason;
    }
}
