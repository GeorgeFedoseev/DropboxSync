

using System;

namespace DBXSync.Model {

    [Serializable]
	public enum DBXErrorType {
		Unknown,
		NotAuthorized,
		FileNotFound,
		BadRequest,
		LocalFileSystemError,
        NetworkProblem,
        DropboxAPIError,
        ParsingError,
        UserCanceled,
        NotSupported,
        AlreadyExists
	}

    [Serializable]
    public class DBXError {
        private string _errorDescription;
        public string ErrorDescription {
            get {
                return _errorDescription;
            }
        }

        private DBXErrorType _errorType = DBXErrorType.Unknown;
        public DBXErrorType ErrorType {
            get {
                return _errorType;
            }

            set {
                _errorType = value;
            }
        }

        public DBXError(string errorDescription, DBXErrorType errorType){
            _errorDescription = errorDescription;
            _errorType = errorType;
        }
    }
}
