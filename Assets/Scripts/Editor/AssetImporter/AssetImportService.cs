using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using sepLog = September.Editor.Logger;

namespace September.Editor.AssetImporter
{
    public class AssetImportService : IAssetImportService
    {
        private readonly IReleaseService _releaseService;
        private readonly IAssetDownloader _assetDownloader;
        private readonly IFileExtractor _fileExtractor;
        private readonly bool _enableLogger;

        public event Action<ProgressInfo> OnProgressChanged;

        public AssetImportService(
            IReleaseService releaseService = null,
            IAssetDownloader assetDownloader = null,
            IFileExtractor fileExtractor = null,
            bool enableLogger = true)
        {
            _enableLogger = enableLogger;
            _releaseService = releaseService ?? new ReleaseService(enableLogger);
            _assetDownloader = assetDownloader ?? new AssetDownloader(enableLogger);
            _fileExtractor = fileExtractor ?? new FileExtractor(enableLogger);

            // 進捗イベントの購読
            if (_assetDownloader is IProgressReporter downloadReporter)
                downloadReporter.OnProgressChanged += ReportProgress;
            if (_fileExtractor is IProgressReporter extractReporter)
                extractReporter.OnProgressChanged += ReportProgress;
        }

        public async UniTask<List<Release>> GetReleasesAsync(string route, CancellationToken cancellationToken)
        {
            try
            {
                return await _releaseService.GetReleasesAsync(route, cancellationToken);
            }
            catch (Exception e)
            {
                sepLog.Logger.LogError($"リリース取得エラー: {e.Message}");
                throw;
            }
        }

        public async UniTask<string> DownloadAndExtractAssetAsync(string route, int assetId, CancellationToken cancellationToken)
        {
            try
            {
                // ダウンロード
                var fileData = await _assetDownloader.DownloadAssetAsync(route, assetId, cancellationToken);

                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.FileSaving,
                    Progress = 1f,
                    Detail = AssetImportConstants.ProgressMessages.FileSaveDetail
                });

                // 一時ファイルの保存
                var tempPath = await SaveTemporaryFile(fileData, assetId, cancellationToken);

                // 解凍
                var extractPath = GetExtractionPath(tempPath);
                sepLog.Logger.LogInfo($"解凍先パス: {extractPath}", _enableLogger);
                
                var result = await _fileExtractor.ExtractZipFileAsync(tempPath, extractPath, cancellationToken);

                // 一時ファイルの削除
                CleanupTemporaryFile(tempPath);

                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.Completed,
                    Progress = 1f,
                    Detail = AssetImportConstants.ProgressMessages.CompletedDetail
                });

                return result;
            }
            catch (Exception e)
            {
                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.Error,
                    Progress = 0f,
                    Detail = $"エラー: {e.Message}"
                });
                throw;
            }
        }

        public void ImportUnityPackages(string extractPath, bool showImportDialog = false)
        {
            try
            {
                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.Importing,
                    Progress = 1f,
                    Detail = AssetImportConstants.ProgressMessages.ImportDetail
                });

                var files = Directory.GetFiles(extractPath, AssetImportConstants.FileExtensions.UnityPackage, SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    sepLog.Logger.LogInfo($"UnityPackageをインポート: {file}", _enableLogger);
                    AssetDatabase.ImportPackage(file, showImportDialog);
                }

                if (!AssetImportSettings.FileChecking)
                {
                    Directory.Delete(extractPath, true);
                    sepLog.Logger.LogInfo($"解凍フォルダを削除: {extractPath}", _enableLogger);
                }
            }
            catch (Exception e)
            {
                var error = $"UnityPackageインポート中にエラーが発生: {e.Message}";
                sepLog.Logger.LogError(error);
                throw new AssetImportException(error, e);
            }
        }

        private async UniTask<string> SaveTemporaryFile(byte[] fileData, int assetId, CancellationToken cancellationToken)
        {
            if (fileData == null || fileData.Length == 0)
            {
                throw new AssetImportException("ダウンロードしたファイルデータが空です");
            }

            // 一意なファイル名を生成
            var fileName = $"Asset_{assetId}_{DateTime.Now:yyyyMMdd_HHmmss}{AssetImportConstants.FileExtensions.Zip}";
            var tempPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            sepLog.Logger.LogInfo($"一時ファイル保存開始: {tempPath} ({fileData.Length} bytes)", _enableLogger);

            await File.WriteAllBytesAsync(tempPath, fileData, cancellationToken);
            
            // ファイルが正常に作成されたか確認
            if (!File.Exists(tempPath))
            {
                throw new AssetImportException($"一時ファイルの作成に失敗しました: {tempPath}");
            }

            var actualSize = new FileInfo(tempPath).Length;
            if (actualSize != fileData.Length)
            {
                throw new AssetImportException($"ファイルサイズの不一致: 期待値={fileData.Length}, 実際={actualSize}");
            }

            sepLog.Logger.LogInfo($"一時ファイル保存完了: {tempPath} ({actualSize} bytes)", _enableLogger);

            return tempPath;
        }

        private static string GetExtractionPath(string zipPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(zipPath);
            
            // ファイル名の清浄化
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            
            // 空の場合のフォールバック
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"ExtractedAsset_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            
            var extractPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            
            // パスが既に存在する場合は番号を付加
            var counter = 1;
            var originalPath = extractPath;
            while (Directory.Exists(extractPath))
            {
                extractPath = $"{originalPath}_{counter}";
                counter++;
            }
            
            return extractPath;
        }

        private void CleanupTemporaryFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    sepLog.Logger.LogInfo($"一時ファイル削除: {filePath}", _enableLogger);
                }
            }
            catch (Exception e)
            {
                sepLog.Logger.LogError($"一時ファイル削除エラー: {e.Message}");
            }
        }

        private void ReportProgress(ProgressInfo progress)
        {
            OnProgressChanged?.Invoke(progress);
        }
    }
}