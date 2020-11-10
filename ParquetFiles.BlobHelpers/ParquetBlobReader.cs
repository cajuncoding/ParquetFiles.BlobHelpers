using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parquet;
using Parquet.Data;
using ParquetFiles.BlobHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ParquetFile.BlobHelpers
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
        protected ParquetReader _parquetReader;

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
            _parquetReader = new ParquetReader(_blobStream);
            return this;
        }

        public DataField[] ReadDataFields()
        {
            var dataFields = _parquetReader.Schema.GetDataFields();
            return dataFields;
        }

        /// <summary>
        /// Provid an IEnumeragle interface to the Parquet.Net Deserialization of Data for efficient
        /// processinga with support for Linq processing if/when needed.
        /// WARNING: Care must be taken to prevent Multiple Enumerations unnecessarily such as being sure
        ///             to project filtered results into a List (e.g. ToList())!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> Read<T>() where T : new()
        {
            AssertParquetReaderIsOpen();

            //It seemst hat the only API that works consistently is this one whereby we loop the RowGroups ourselves
            //  but this also provies a little more control over the IEnumerable processing by not forcing all rows & row-groups
            //  to be in memory at one time.
            for (int g = 0; g < _parquetReader.RowGroupCount; g++)
            {
                LogDebug($"Enumerating over RowGroup #[{g}]...");
                var timer = Stopwatch.StartNew();

                var group = ParquetConvert.Deserialize<T>(_blobStream, g);

                timer.Stop();
                LogDebug($"Deserialized RowGroup [{g}] from the Stream in [{timer.ToElapsedTimeDescriptiveFormat()}].");

                foreach (var item in group)
                {
                    yield return item;
                }
            }
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

            //Downlaod the Data from Blob to the Stream and return the Readable/Seekable stream for processing...
            LogDebug($"Downloading Data ~[{blobSizeInMB} MB] into the local stream...");
            await blobClient.DownloadToAsync(blobStream);
            LogDebug($"Successfully downloaded the Blob data in [{timer.ToElapsedTimeDescriptiveFormat()}]...");
            
            return blobStream;
        }
       
        protected virtual void LogDebug(string message)
        {
            this.Options?.LogDebug?.Invoke(message);
        }

        protected virtual void AssertParquetReaderOptionsAreValid()
        {
            if (this.Options == null)
            {
                throw new InvalidOperationException("The Parquet Blob Reader options are invalid or not initialized.");
            }
        }

        protected virtual void AssertParquetReaderIsOpen()
        {
            AssertParquetReaderOptionsAreValid();
            
            if (_parquetReader == null || _blobStream == null)
            {
                throw new InvalidOperationException($"The Parque Reader has not been initialized; " +
                    $"ensure that {nameof(OpenAsync)} has been correctly called.");
            }
        }

        public void Dispose()
        {
            //NOTE: We MUST dispose of of items in this order to eliminate issues with Locks and risk of leaving
            //      the stream (e.g. file) with a current lock, etc.
            _parquetReader?.Dispose();
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
