using ParquetFile.BlobHelpers;
using ParquetFile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var blobStorageConnectionString = Config.AzureBlobStorageConnectionString;
            var blobContainer = Config.BlobContainerName;
            var blobFilePath = Config.BlobFilePath;

            var options = new ParquetBlobReaderOptions()
            {
                LogDebug = (message) => Console.WriteLine(message)
            };

            using (var parquetReader = await new ParquetBlobReader(blobStorageConnectionString, blobContainer, blobFilePath, options).OpenAsync())
            { 
                var validStatuses = new List<int?> { 1, 2, 3 };

                //Example of Reading a Parquet File into the specified Model (by Generic Type) and enumerating the results.
                //We also implement a Linq Filter to illustrate working with IEnumerable and Linq.
                //NOTE: We must project the filtered results into a List (since we are pre-filtering)
                //          this guarantees that we do not enumerate the results twice (multiple-enumeration):
                //          First for the Count() and then for the Foreach Loop...
                var results = parquetReader.Read<ItemModel>()
                                .Where(e => validStatuses.Contains(e.StatusId))
                                .ToList();

                var x = 1;
                Console.WriteLine($"[{results.Count()}] Valid Items found after Filtering!");
                foreach (var item in results)
                {
                    Console.WriteLine($"{x++}) {item.Id} -- {item.Name} [Status={item.StatusId}]");
                }
            }

        }
    }
}
