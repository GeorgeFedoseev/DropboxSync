namespace DBXSync {

	[System.Serializable]
    public class GetMetadataRequestParameters : RequestParameters {
        public string path;	
		public bool include_media_info = false;
		public bool include_deleted = false;
		public bool include_has_explicit_shared_members = false;
		
    }

}