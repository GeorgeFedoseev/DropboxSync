namespace DBXSync {
    public class DropboxAPIException : System.Exception {
        public string error_summary = null;
        public string error_tag = null;        

        public DropboxAPIException(string message, string error_summary, string error_tag) : base(message){
            this.error_summary = error_summary;
            this.error_tag = error_tag;
        }

        public DropboxAPIException(string message) : base(message){
            this.error_summary = message;
            this.error_tag = null;
        }
    }
}