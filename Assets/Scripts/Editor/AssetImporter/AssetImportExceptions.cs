using System;

namespace September.Editor.AssetImporter
{
    public class AssetImportException : Exception
    {
        public AssetImportException() { }
        public AssetImportException(string message) : base(message) { }
        public AssetImportException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class AssetDownloadException : AssetImportException
    {
        public AssetDownloadException() { }
        public AssetDownloadException(string message) : base(message) { }
        public AssetDownloadException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class AssetExtractionException : AssetImportException
    {
        public AssetExtractionException() { }
        public AssetExtractionException(string message) : base(message) { }
        public AssetExtractionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ReleaseServiceException : AssetImportException
    {
        public ReleaseServiceException() { }
        public ReleaseServiceException(string message) : base(message) { }
        public ReleaseServiceException(string message, Exception innerException) : base(message, innerException) { }
    }
}