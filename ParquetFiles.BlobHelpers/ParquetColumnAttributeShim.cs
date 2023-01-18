using System;

//NOTE: For compatibility with v4.2+ we use the root namespace!
namespace Parquet
{
    /// <summary>
    /// This attribute annotation is functionally identical to ParquetColumnAttribute (provided in the lower level library)
    /// but provides a compatibility shim for v4.1.3 that is the same as the newer v4.2+ versions with the attributes in the root
    /// Parquet namespace.
    /// 
    /// NOTE: Normally the moving of the attribute to a new namespace changes would be normal process and require a one time migration however,
    ///     there are significant/critical bugs in v4.2+ that prevent it from working such as DateTime columns failing to load
    ///     null values (see: https://github.com/aloneguid/parquet-dotnet/issues/224). And if you have already migrated your namespaces
    ///     then it is a real pain to revert them; therefore this shim makes v4.1.3 compatible!
    ///     .
    /// </summary>
    public class ParquetColumnAttribute : Parquet.Attributes.ParquetColumnAttribute
    {
        public ParquetColumnAttribute(string name) : base(name)
        { }
    }
}
