namespace DBXSync {
    public class DropboxNotFoundAPIException : DropboxAPIException {        

        public DropboxNotFoundAPIException(string message, string error_summary, string error_tag) : base(message, error_summary, error_tag) {}
    }
}