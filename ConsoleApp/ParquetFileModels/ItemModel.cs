using Parquet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ParquetFile.Models
{
    public class ItemModel
    {
        [ParquetColumn("id")]
        public long? Id { get; set; }
        
        [ParquetColumn("name")]
        public string? Name { get; set; }

        [ParquetColumn("status")]
        public int? StatusId { get; set; }

        public override string ToString()
        {
            return $"{Id}::{Name}";
        }
    }
}
