namespace DBXSync {

    public class ListFolderLongpollResponse : Response {
        public bool changes;
        public int backoff = 0;
    }
}