namespace DBXSync {

    public class ListFolderLongpollRequestParameters : RequestParameters {
        public string cursor;
        public int timeout = 30;        
    }
}