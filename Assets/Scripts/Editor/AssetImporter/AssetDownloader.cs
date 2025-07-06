using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using sepLog = September.Editor.Logger;

namespace September.Editor.AssetImporter
{
    public class AssetDownloader : IAssetDownloader, IProgressReporter
    {
        private readonly bool _enableLogger;

        public event Action<ProgressInfo> OnProgressChanged;

        public AssetDownloader(bool enableLogger = true)
        {
            _enableLogger = enableLogger;
        }

        public async UniTask<byte[]> DownloadAssetAsync(string route, int assetId, CancellationToken cancellationToken)
        {
            var url = $"{AssetImportConstants.ApiUrl}/{route}?id={assetId}";

            ReportProgress(new ProgressInfo
            {
                Status = AssetImportConstants.ProgressMessages.DownloadStarting,
                Progress = 0f,
                Detail = AssetImportConstants.ProgressMessages.DownloadStartDetail
            });

            using (var request = UnityWebRequest.Get(url))
            {
                try
                {
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        var progress = request.downloadProgress;
                        ReportProgress(new ProgressInfo
                        {
                            Status = AssetImportConstants.ProgressMessages.Downloading,
                            Progress = progress,
                            Detail = $"ダウンロード進捗: {progress * 100:F1}%"
                        });

                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }

                    await operation.ToUniTask(cancellationToken: cancellationToken);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var error = $"ダウンロードエラー: {request.error}";
                        sepLog.Logger.LogError(error);
                        throw new AssetDownloadException(error);
                    }

                    sepLog.Logger.LogInfo($"アセットダウンロード完了: ID={assetId}", _enableLogger);
                    return request.downloadHandler.data;
                }
                catch (Exception e) when (!(e is AssetDownloadException))
                {
                    var error = $"ダウンロード中に予期しないエラーが発生: {e.Message}";
                    sepLog.Logger.LogError(error);
                    throw new AssetDownloadException(error, e);
                }
            }
        }

        public void ReportProgress(ProgressInfo progress)
        {
            OnProgressChanged?.Invoke(progress);
        }
    }
}