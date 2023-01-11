using ParquetFile.BlobHelpers;
using ParquetFile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Parquet;


namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var config = new ParquetReaderConsoleAppConfig();
            var options = new ParquetBlobReaderOptions() { LogDebug = Console.WriteLine };

            //Reader is IDisposable!
            using var parquetBlobReader = await new ParquetBlobReader(
                config.AzureBlobStorageConnectionString,
                config.BlobContainerName,
                config.BlobFilePath, 
                options
            ).OpenAsync();

            //Example of Reading a Parquet File into the specified Model (by Generic Type) and enumerating the results.
            //We also implement a Linq Filter to illustrate working with IEnumerable and Linq.
            //NOTE: We must project the filtered results into a List (since we are pre-filtering)
            //          this guarantees that we do not enumerate the results twice (multiple-enumeration):
            //          First for the Count() and then for the Foreach Loop...
            var results = (await parquetBlobReader.ReadAllAsync<ItemModel>()).OrderBy(r => r.Id);

            var x = 1;
            Console.WriteLine($"[{results.Count()}] Valid Items found!");
            foreach (var item in results)
            {
                //Console.WriteLine($"{x++}) {item.Id} -- {item.Category} [Budget={item.Budget}; InternalCost={item.InternalCost}]");
                Console.WriteLine($"{x++}) {item}");
            }

            Console.ReadKey();
        }

        public class FCModel
        {
            [ParquetColumn("name")]
            public string? Name { get; set; }

            [ParquetColumn("budget")]
            public decimal? Budget { get; set; }
        }
    }
}
