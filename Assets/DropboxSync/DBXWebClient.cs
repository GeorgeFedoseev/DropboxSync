using System;
using System.Net;


namespace DBXSync {
    public class DBXWebClient: WebClient {
        public int Timeout = 15000;   

        protected override WebRequest GetWebRequest(Uri uri) {
            var lWebRequest = base.GetWebRequest(uri);

            if(lWebRequest != null){
                lWebRequest.Timeout = Timeout;  
            }

            // fixes as per http://vikeed.blogspot.com/2011/03/uploading-large-files-using-http-put-in.html
            // fix for Concurrent IO operations
            ((HttpWebRequest)lWebRequest).AllowWriteStreamBuffering = false;
            ((HttpWebRequest)lWebRequest).SendChunked = false;
            

            return lWebRequest;
        }
    }
}

