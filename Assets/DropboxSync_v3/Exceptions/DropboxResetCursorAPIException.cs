namespace DBXSync {
    public class DropboxResetCursorAPIException : DropboxAPIException {        

        public DropboxResetCursorAPIException(string message, string error_summary, string error_tag) : base(message, error_summary, error_tag) {}
    }
}