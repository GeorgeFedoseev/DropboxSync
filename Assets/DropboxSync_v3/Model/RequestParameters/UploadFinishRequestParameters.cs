namespace DBXSync {

	[System.Serializable]
    public class UploadFinishRequestParameters : RequestParameters {
        public Cursor cursor;
        public CommitInfo commit;

        public UploadFinishRequestParameters(string session_id, long offset, string path){
            cursor = new Cursor();            
            cursor.session_id = session_id;
            cursor.offset = offset;
            
            commit = new CommitInfo();
            commit.path = path;            
            commit.mode = "overwrite";
        }
    }

}