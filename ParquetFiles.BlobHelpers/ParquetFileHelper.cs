using System;

namespace ParquetFiles.BlobHelpers
{
    public static class ParquetFileHelper
    {
        public static int BYTES_PER_MEGABYTE = (int)Math.Pow(1024.0, 2.0);

        public static double BytesToMegabytes(long byteCount)
        {
            return (double)byteCount / BYTES_PER_MEGABYTE;
        }
    }
}
