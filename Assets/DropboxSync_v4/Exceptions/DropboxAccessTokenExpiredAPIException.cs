namespace DBXSync {
    public class DropboxAccessTokenExpiredAPIException : DropboxAPIException {

        public DropboxAccessTokenExpiredAPIException(string message, string error_summary, string error_tag) : base(message, error_summary, error_tag) { }
    }
}