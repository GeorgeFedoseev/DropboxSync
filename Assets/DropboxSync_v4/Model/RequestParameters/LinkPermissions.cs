using System.Collections.Generic;

namespace DBXSync {
    [System.Serializable]
    public class ResolvedVisibility {
        public string tag;
    }

    [System.Serializable]
    public class RequestedVisibility {
        public string tag;
    }

    [System.Serializable]
    public class LinkPermissions : JSONSerializableObject {
        public ResolvedVisibility resolved_visibility;
        public RequestedVisibility requested_visibility;
        public bool can_revoke;
        public List<VisibilityPolicy> visibility_policies;
        public bool can_set_expiry;
        public bool can_remove_expiry;
        public bool allow_download;
        public bool can_allow_download;
        public bool can_disallow_download;
        public bool allow_comments;
        public bool team_restricts_comments;
        public List<AudienceOption> audience_options;
        public bool can_set_password;
        public bool can_remove_password;
    }
}
