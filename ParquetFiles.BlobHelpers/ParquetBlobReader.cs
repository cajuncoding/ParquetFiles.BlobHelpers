using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parquet;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ParquetFiles.BlobHelpers
{
    public class ParquetBlobReaderOptions
    {
        public int MemoryStreamLimitMegabytes { get; set; } = 250;
        public long MemoryStreamLimitThresholdBytes => (long)(MemoryStreamLimitMegabytes * ParquetFileHelper.BYTES_PER_MEGABYTE);
        public Action<string> LogDebug { get; set; } = null;
        public bool LoggingEnabled => LogDebug != null;
    }

    public class ParquetBlobReader : IDisposable
    {
        public BlobContainerClient BlobContainerClient { get; protected set; }
        public String BlobContainerName { get; protected set; }
        public String BlobFilePath { get; protected set; }
        public ParquetBlobReaderOptions Options { get; protected set; }

        protected Stream _blobStream;
        protected FileInfo _tempFileInfo;

        public ParquetBlobReader(string blobConnectionString, string blobContainerName, string blobFilePath, ParquetBlobReaderOptions options = null)
        {
            BlobContainerClient = new BlobContainerClient(blobConnectionString, blobContainerName);
            BlobContainerName = blobContainerName?.Trim(Path.AltDirectorySeparatorChar);
            BlobFilePath = blobFilePath?.Trim(Path.AltDirectorySeparatorChar);
            Options = options ?? new ParquetBlobReaderOptions();
        }

        public async Task<ParquetBlobReader> OpenAsync(CancellationToken cancellationToken = default)
        {
            _blobStream = await CreateBlobContentStreamAsync(cancellationToken);
            return this;
        }

        /// <summary>
        /// Provide an IEnumerable interface to the Parquet.Net Deserialization of Data for efficient
        /// processing with support for Linq processing if/when needed.
        /// WARNING: Care must be taken to prevent Multiple Enumerations unnecessarily such as being sure
        ///             to project filtered results into a List (e.g. ToList())!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<T[]> ReadAllAsync<T>() where T : new()
        {
            //Now in the updated Parquet.NET API v4+ they now provide fully Async methods, but only provide
            //  the ability to read all data in one request (so we can no longer easily yeild by groups).
            //Therefore, to be consistent with he underlying API we switch back to their approach to ensure we are using
            //  as much of hte underlying API as we can, and hope they will improve the API in the future to be more flexible.
            var timer = Stopwatch.StartNew();

            var results = await ParquetConvert.DeserializeAsync<T>(_blobStream);

            timer.Stop();
            LogDebug($"Deserialized [{results.Length}] items from the Stream in [{timer.ToElapsedTimeDescriptiveFormat()}].");
            
            return results;
        }

        protected virtual async Task<Stream> CreateBlobContentStreamAsync(CancellationToken cancellationToken)
        {
            AssertParquetReaderOptionsAreValid();
            var timer = Stopwatch.StartNew();

            LogDebug($"Reading Blob Info for [{BlobFilePath}]...");
            
            BlobClient blobClient = BlobContainerClient.GetBlobClient(BlobFilePath);
            BlobProperties blobInfo = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            
            LogDebug($"Blob Info retrieved successfully in [{timer.ToElapsedTimeDescriptiveFormat()}]");

            long blobContentLength = blobInfo.ContentLength;
            long blobSizeInMB = (long)ParquetFileHelper.BytesToMegabytes(blobContentLength);
            
            LogDebug($"Initializing local stream for Blob data...");
            Stream blobStream;

            //Use File Stream for stability when the data size is greater than the configured size threshold
            //  for in-memory processing; otherwise use MemoryStream for performance...
            if (blobContentLength > this.Options.MemoryStreamLimitThresholdBytes)
            {
                //Initialize the Internal Reference to the Temp File we have initialized!
                //NOTE: THIS MUST be cleaned up when the class is Disposed!
                var tempFilePath = Path.GetTempFileName();
                _tempFileInfo = new FileInfo(tempFilePath);

                LogDebug($"Initialized Temp File [{tempFilePath}]...");

                blobStream = _tempFileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);

                LogDebug($"File Stream created successfully...");
            }
            else
            {
                //Use a MemoryStream for performance when not over our configured cutoff size...
                blobStream = new MemoryStream();
                LogDebug($"Memory Stream created successfully...");
            }

            //Download the Data from Blob to the Stream and return the Readable/Seekable stream for processing...
            LogDebug($"Downloading Data ~[{blobSizeInMB} MB] into the local stream...");
            await blobClient.DownloadToAsync(blobStream, cancellationToken);
            LogDebug($"Successfully downloaded the Blob data in [{timer.ToElapsedTimeDescriptiveFormat()}]...");
            
            return blobStream;
        }
       
        protected virtual void LogDebug(string message)
        {
            if(this.Options.LoggingEnabled)
                this.Options.LogDebug.Invoke(message);
        }

        protected virtual void AssertParquetReaderOptionsAreValid()
        {
            if (this.Options == null)
            {
                throw new InvalidOperationException("The Parquet Blob Reader options are invalid or not initialized.");
            }
        }

        public void Dispose()
        {
            //NOTE: We MUST dispose of of items in this order to eliminate issues with Locks and risk of leaving
            //      the stream (e.g. file) with a current lock, etc.
            _blobStream?.Dispose();

            if(_tempFileInfo != null)
            {
                //NOTE: This should be safe per MS Docs: "If the file to be deleted does not exist, no exception is thrown."
                //  as long as we ensure the parameter is not null and is a full file path and not a folder path, etc.
                _tempFileInfo.Delete();
            }
        }
    }

    public static class TimeSpanCustomExtensions
    {
        public static string ToElapsedTimeDescriptiveFormat(this Stopwatch timer)
        {
            var timeSpan = timer.Elapsed;
            var descriptiveFormat = $"{timeSpan:hh\\h\\:mm\\m\\:ss\\s\\:fff\\m\\s}";
            return descriptiveFormat;
        }
    }
}
