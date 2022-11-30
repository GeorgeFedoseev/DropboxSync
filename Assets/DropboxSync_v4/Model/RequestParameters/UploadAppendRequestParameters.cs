namespace DBXSync {

	[System.Serializable]
    public class UploadAppendRequestParameters : RequestParameters {
        public Cursor cursor;
        public bool close = false;

        public UploadAppendRequestParameters(string session_id, long offset){
            cursor = new Cursor();
            cursor.session_id = session_id;
            cursor.offset = offset;                        
        }
    }

}