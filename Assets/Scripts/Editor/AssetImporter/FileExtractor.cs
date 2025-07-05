using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using sepLog = September.Editor.Logger;

namespace September.Editor.AssetImporter
{
    public class FileExtractor : IFileExtractor, IProgressReporter
    {
        private readonly bool _enableLogger;

        public event Action<ProgressInfo> OnProgressChanged;

        public FileExtractor(bool enableLogger = true)
        {
            _enableLogger = enableLogger;
        }

        public async UniTask<string> ExtractZipFileAsync(string zipPath, string extractPath, CancellationToken cancellationToken)
        {
            try
            {
                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.ExtractionStarting,
                    Progress = 0f,
                    Detail = AssetImportConstants.ProgressMessages.ExtractionStartDetail
                });

                Directory.CreateDirectory(extractPath);

                await Task.Run(() => ExtractWithProgress(zipPath, extractPath, cancellationToken), cancellationToken);

                sepLog.Logger.LogInfo($"ZIPファイル解凍完了: {zipPath} -> {extractPath}", _enableLogger);

                ReportProgress(new ProgressInfo
                {
                    Status = AssetImportConstants.ProgressMessages.Completed,
                    Progress = 1f,
                    Detail = "解凍が完了しました"
                });

                return extractPath;
            }
            catch (Exception e) when (!(e is AssetExtractionException))
            {
                var error = $"解凍中にエラーが発生: {e.Message}";
                sepLog.Logger.LogError(error);
                throw new AssetExtractionException(error, e);
            }
        }

        private void ExtractWithProgress(string zipPath, string extractPath, CancellationToken cancellationToken)
        {
            sepLog.Logger.LogInfo($"ZIP解凍開始: {zipPath} -> {extractPath}", _enableLogger);

            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException($"ZIPファイルが見つかりません: {zipPath}");
            }

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var totalEntries = archive.Entries.Count;
                var extractedEntries = 0;

                sepLog.Logger.LogInfo($"ZIP内のエントリ数: {totalEntries}", _enableLogger);

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var sanitizedName = SanitizeFileName(entry.FullName);
                    var destinationPath = Path.Combine(extractPath, sanitizedName);

                    // パスの正規化とセキュリティチェック
                    destinationPath = Path.GetFullPath(destinationPath);
                    if (!destinationPath.StartsWith(Path.GetFullPath(extractPath)))
                    {
                        if (_enableLogger) sepLog.Logger.LogError($"不正なパス検出をスキップ: {entry.FullName}");
                        continue;
                    }

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        // ディレクトリエントリ
                        if (!Directory.Exists(destinationPath))
                        {
                            Directory.CreateDirectory(destinationPath);
                            sepLog.Logger.LogInfo($"ディレクトリ作成: {destinationPath}", _enableLogger);
                        }
                    }
                    else
                    {
                        // ファイルエントリ
                        var directoryPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(directoryPath);
                                sepLog.Logger.LogInfo($"親ディレクトリ作成: {directoryPath}", _enableLogger);
                            }
                            catch (Exception dirEx)
                            {
                                if (_enableLogger) sepLog.Logger.LogError($"ディレクトリ作成エラー: {directoryPath} - {dirEx.Message}");
                                extractedEntries++;
                                continue;
                            }
                        }

                        // ファイルサイズ0の場合はスキップ（一部のZIPで空ファイルがある）
                        if (entry.Length == 0)
                        {
                            sepLog.Logger.LogInfo($"空ファイルをスキップ: {entry.FullName}", _enableLogger);
                        }
                        else
                        {
                            try
                            {
                                entry.ExtractToFile(destinationPath, true);
                                sepLog.Logger.LogInfo($"ファイル解凍: {destinationPath} ({entry.Length} bytes)", _enableLogger);
                            }
                            catch (Exception fileEx)
                            {
                                if (_enableLogger) sepLog.Logger.LogError($"ファイル解凍エラー: {destinationPath} - {fileEx.Message}");
                                extractedEntries++;
                                continue;
                            }
                        }
                    }

                    extractedEntries++;
                    var progress = (float)extractedEntries / totalEntries;

                    // 進捗報告の頻度を制限（パフォーマンス向上）
                    if (extractedEntries % 10 == 0 || extractedEntries == totalEntries)
                    {
                        ReportProgress(new ProgressInfo
                        {
                            Status = AssetImportConstants.ProgressMessages.Extracting,
                            Progress = progress,
                            Detail = $"解凍進捗: {extractedEntries}/{totalEntries} ({progress * 100:F1}%)"
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (_enableLogger) sepLog.Logger.LogError($"エントリ解凍エラー: {entry.FullName} - {ex.Message}");
                    // 個別のエントリエラーは継続処理
                    extractedEntries++;
                    continue;
                }
            }

            sepLog.Logger.LogInfo($"ZIP解凍完了: {extractedEntries}/{totalEntries} エントリ処理完了", _enableLogger);
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unnamed";

            // 危険な文字を除去
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var invalidPathChars = Path.GetInvalidPathChars();
            
            foreach (var invalidChar in invalidFileNameChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            
            foreach (var invalidChar in invalidPathChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            // 先頭の区切り文字を除去
            fileName = fileName.TrimStart('/', '\\');
            
            // 相対パス攻撃を防ぐ
            fileName = fileName.Replace("..", "_");

            // 空になった場合のフォールバック
            if (string.IsNullOrWhiteSpace(fileName))
                return "unnamed";

            return fileName;
        }

        public void ReportProgress(ProgressInfo progress)
        {
            OnProgressChanged?.Invoke(progress);
        }
    }
}