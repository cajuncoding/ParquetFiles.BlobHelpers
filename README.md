# ParquetFile.BlobHelpers
This is a simple library and example console application to illustrate how to read and load data into class models from 
Parquet files saved to Azure Blob Storage using [Parquet .Net (parquet-dotnet)](https://github.com/elastacloud/parquet-dotnet#apache-parquet-for-net-platform).

## Overview
This is useful for E-L-T (extract-load-transform) processes whereby you need to load the data into Memory, 
Sql Server (e.g. Azure SQL), etc. or any other location where there is no built-in or default mechanism for 
working with Parquet data.

### [Buy me a Coffee ☕](https://www.buymeacoffee.com/cajuncoding)
*I'm happy to share with the community, but if you find this useful (e.g for professional use), and are so inclinded,
then I do love-me-some-coffee!*

<a href="https://www.buymeacoffee.com/cajuncoding" target="_blank">
<img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174">
</a> 

## Nuget Package
To use in your project, add the [ParquetFile.BlobHelpers NuGet package](https://www.nuget.org/packages/ParquetFiles.BlobHelpers) to your project.

### v2.0 Release Notes:
- Updated to use the latest Parquet.NET v4.2.2
- Breaking changes in the underlying Parquet.NET necessitated breaking changes here in our API:
    - Mainly `Read<T>()` is now `ReadAllAsync<T>()` -- fully async but no longer supports yielding the enumerable due to limitations in the underlying `ParquetConvert` class at this time.

### v1.0 Release Notes:
- Initial stable functioning release.

### Details
As noted in the Parquet-DotNet documentation, processing data from a parquet file requires alot of seeking and therefore
requires that the file be provided in a readable and seekable Stream! This precludes the ability to read data
while streaming down from Blob in real-time -- the entire file must be locally available.

This library provides ability to use either MemoryStream or FileStream depending on the size of the parquet file
by setting a threshold limit the denotes the maximum size of which a MemoryStream should be used.  This allows
for high perfomrance of MemoryStreams whenever possible, but enabling FileStream anytime the environment has
more constrained memory (e.g. Azure Functions with only 1GB RAM).

When necessary, per configuration, the FileStream work is fully encapsulated but overridable if needed via virtual method. 
A local temp file is created and used for managing the stream of data. Then the stream, as well as the temp file, is 
automatically cleaned up as soon as the `ParquetBlobReader` is properly disposed -- which it must be via IDisposable.

*Note: We leverage the [**Fast Automatic Serialization functionality**](https://github.com/elastacloud/parquet-dotnet/blob/master/doc/serialisation.md) 
 built in functionality of Parquet-DotNet to Deserialise the data from the Parquet file into class Models -- 
`ParquetConvert.DeserializeAsync<TModel>(...)`.  This is convenient and initial testing shows solid performance (as expected).
However it seems to have alot of dependencies on models with Nullable properties, and can actual load data 
incorrectly when they aren't nullable.  So pending further testinga and real world usage we may have to 
implement our own processing of the data....*

### Dependencies
1. [Parquet-DotNet](https://github.com/elastacloud/parquet-dotnet#apache-parquet-for-net-platform) -- The goto Parquet File reader for C# & .Net.
2. Azure.Storage.Blobs

## Use Case Example (E-L-T)
We use this for loading data into Azure Sql via Azure Functions that handle our data-load processes. Unfortunately,
Azure Sql does not have native support for efficiently reading Parquet data like Azure Sql Warehouse or 
Azure Synapse Analytics (the new DW re-branding) -- the OpenRowset() function in DW and Azure Sql Analytics
has unique functionality that makes working with Parquet files much easier.

But, when Parquet is used as a data transport mechanism for API's, Micro-services, etc. then this becomes an issue
as Azure SQL is far appropriate to use as the persistence DB than DW.

And, there isn't alot of information out there about how to easily load a file from Blob storage and read it
with [Parquet.Net (parquet-dotnet)](https://github.com/elastacloud/parquet-dotnet#apache-parquet-for-net-platform);
 which is the go-to parquet file reader for C# and .Net. 

### Loading into Azure Sql (or other system)
By using a Model based approach to reading the data, this makes it exceptionally easy to then pipe that data 
into Azure Sql via your ORM or for high performance using SqlBulkHelpers (if you're a Dapper User) or RepoDb 
(which has a very similar Bulk Insert/Update capability OOTB)!

For example, if you're loading data with a Materialized Data pattern, and also using a serverless environment, 
such as Azure Functions, you can can use a distributed lock to ensure that only one process runs at a time
and then load and instantly switch out data into Live loading tables with these other libraries:
1. [Materialized Data Helpers via SqlBulkHelpers Library](https://github.com/cajuncoding/SqlBulkHelpers)
   - Accomplish a near instantaneous live Table refresh leaving the Live tables unblocked until all data is laoded and ready to be switched (in millesconds)!
2. [Implement a Distributed Mutex Application Lock via SqlAppLockHelper Library](https://github.com/cajuncoding/SqlAppLockHelper)
   - Implement background (fully asynchronous) data loading for Materialization of data and ensrue that only one process ever runs at a time with Serverless resources such as Azure Functions! 

## Example Usage
```csharp
    //Initialize Options (here we wire up a simple log handler to redirect to the Console)...
    var options = new ParquetBlobReaderOptions()
    {
        //Easily/Optionally direct logging wherever you want with a handler Action<string>...
        LogDebug = (message) => Console.WriteLine(message)
    };

    //Create an instance of ParquetBlobReader() which MUST be Disposed of properly...
    using (var parquetReader = new ParquetBlobReader(blobStorageConnectionString, blobContainer, blobFilePath, options))
    { 
        //Open & initialize the Blob Stream (this will download the blob data)...
        await parquetReader.OpenAsync();

        //Read All Results and filter, sort, etc.
        var results = (await parquetReader.ReadAllAsync<ItemModel>()).OrderBy(r => r.Id);
        
        //Example of Reading a Parquet File into the specified Model (by Generic Type) 
        // and enumerating the results provided by the IEnumerable result...
        var x = 1;
        foreach (var item in results)
        {
            Console.WriteLine($"{x++}) {item.Id} -- {item.Name}");
        }
    }
```
