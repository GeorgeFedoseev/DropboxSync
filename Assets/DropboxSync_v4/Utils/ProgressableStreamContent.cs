using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DBXSync {
    internal class ProgressableStreamContent : HttpContent
    {
        private const int defaultBufferSize = 4096;

        private Stream sourceStream;
        private int bufferSize;
        private bool contentConsumed;
        private Func<Stream, Stream, Task> _serializeToStreamCallback;

        public ProgressableStreamContent(Stream sourceStream, Func<Stream, Stream, Task>  serializeToStreamCallback) : this(sourceStream, defaultBufferSize, serializeToStreamCallback) {}

        public ProgressableStreamContent(Stream sourceStream, int bufferSize, Func<Stream, Stream, Task>  serializeToStreamCallback)
        {
            if(sourceStream == null)
            {
                throw new ArgumentNullException("content");
            }
            if(bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.sourceStream = sourceStream;
            this.bufferSize = bufferSize;
            this._serializeToStreamCallback = serializeToStreamCallback;
        }

        protected override Task SerializeToStreamAsync(Stream uploadStream, TransportContext context)
        {            
            Contract.Assert(uploadStream != null);

            PrepareContent();

            

            
            return _serializeToStreamCallback(sourceStream, uploadStream);            
            

            

            // return Task.Run(() =>
            // {
                // await _serializeToStreamCallback(sourceStream, uploadStream);

                // var buffer = new Byte[this.bufferSize];
                // var size = content.Length;
                // var uploaded = 0;

                // downloader.ChangeState(DownloadState.PendingUpload);

                // using(content) while(true)
                // {
                //     var length = content.Read(buffer, 0, buffer.Length);
                //     if(length <= 0) break;

                //     downloader.Uploaded = uploaded += length;

                //     stream.Write(buffer, 0, length);

                //     downloader.ChangeState(DownloadState.Uploading);
                // }

                // downloader.ChangeState(DownloadState.PendingResponse);
            // });
        }

        protected override bool TryComputeLength(out long length)
        {
            length = sourceStream.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                sourceStream.Dispose();
            }
            base.Dispose(disposing);
        }


        private void PrepareContent()
        {
            if(contentConsumed)
            {
                // If the content needs to be written to a target stream a 2nd time, then the stream must support
                // seeking (e.g. a FileStream), otherwise the stream can't be copied a second time to a target 
                // stream (e.g. a NetworkStream).
                if(sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }
                else
                {
                    throw new InvalidOperationException("SR.net_http_content_stream_already_read");
                }
            }

            contentConsumed = true;
        }
    }
}