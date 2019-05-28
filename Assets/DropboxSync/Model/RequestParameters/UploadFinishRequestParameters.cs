namespace DBXSync {

	[System.Serializable]
    public class UploadFinishRequestParameters : RequestParameters {
        public Cursor cursor;
        public CommitInfo commit;

        public UploadFinishRequestParameters(string session_id, string path){
            cursor = new Cursor();
            cursor.session_id = session_id;
            commit = new CommitInfo();
            commit.mode = "overwrite";
        }
    }

}