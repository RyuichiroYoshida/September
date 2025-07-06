using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace September.Editor.AssetImporter
{
    public struct ProgressInfo
    {
        public string Status { get; set; }
        public float Progress { get; set; }
        public string Detail { get; set; }
    }

    public interface IProgressReporter
    {
        event Action<ProgressInfo> OnProgressChanged;
        void ReportProgress(ProgressInfo progress);
    }

    public interface IAssetDownloader
    {
        UniTask<byte[]> DownloadAssetAsync(string route, int assetId, CancellationToken cancellationToken);
    }

    public interface IFileExtractor
    {
        UniTask<string> ExtractZipFileAsync(string zipPath, string extractPath, CancellationToken cancellationToken);
    }

    public interface IReleaseService
    {
        UniTask<List<Release>> GetReleasesAsync(string route, CancellationToken cancellationToken);
    }

    public interface IAssetImportService
    {
        UniTask<List<Release>> GetReleasesAsync(string route, CancellationToken cancellationToken);
        UniTask<string> DownloadAndExtractAssetAsync(string route, int assetId, CancellationToken cancellationToken);
        void ImportUnityPackages(string extractPath, bool showImportDialog = false);
        event Action<ProgressInfo> OnProgressChanged;
    }

    public interface IAssetImportController
    {
        bool IsImporting { get; }
        List<string> ReleaseNames { get; }
        int SelectedReleaseIndex { get; set; }

        UniTask InitializeAsync();
        UniTask ImportSelectedAssetAsync();

        event Action<ProgressInfo> OnProgressChanged;
        event Action<string> OnStatusChanged;
    }
}