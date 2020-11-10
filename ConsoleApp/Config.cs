using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleApp

{
    internal class Config
    {

        private static readonly IConfigurationRoot _config;

        static Config()
        {
            // build config
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }

        public static string AzureBlobStorageConnectionString => _config[nameof(AzureBlobStorageConnectionString)];
        public static string BlobContainerName => _config[nameof(BlobContainerName)];
        public static string BlobFilePath => _config[nameof(BlobFilePath)];

    }
}
