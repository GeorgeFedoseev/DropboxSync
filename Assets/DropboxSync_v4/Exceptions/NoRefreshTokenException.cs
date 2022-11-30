namespace DBXSync {
    public class NoRefreshTokenException : System.Exception {        

        public NoRefreshTokenException(string message) : base(message) {}
    }
}