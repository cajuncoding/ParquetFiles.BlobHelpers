using Microsoft.Extensions.Configuration;
using System.IO;

namespace ConsoleApp
{
    internal class ParquetReaderConsoleAppConfig
    {
        private readonly IConfigurationRoot _config;

        public ParquetReaderConsoleAppConfig()
        {
            // build config
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
        }

        public string AzureBlobStorageConnectionString => _config[nameof(AzureBlobStorageConnectionString)];
        public string BlobContainerName => _config[nameof(BlobContainerName)];
        public string BlobFilePath => _config[nameof(BlobFilePath)];

    }
}
