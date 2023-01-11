using System;
using Parquet;

namespace ParquetFile.Models
{
    public class ItemModel
    {
        [ParquetColumn("ID")]
        public int? Id { get; set; }
        
        [ParquetColumn("Name")]
        public string? Name { get; set; }

        public override string ToString()
        {
            return $"{Id}::{Name}";
        }
    }
}
