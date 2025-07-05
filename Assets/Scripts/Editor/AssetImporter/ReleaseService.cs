using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;
using sepLog = September.Editor.Logger;

namespace September.Editor.AssetImporter
{
    public class ReleaseService : IReleaseService
    {
        private readonly bool _enableLogger;

        public ReleaseService(bool enableLogger = true)
        {
            _enableLogger = enableLogger;
        }

        public async UniTask<List<Release>> GetReleasesAsync(string route, CancellationToken cancellationToken)
        {
            var url = $"{AssetImportConstants.ApiUrl}/{route}";

            using (var request = UnityWebRequest.Get(url))
            {
                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var error = $"リリース情報取得エラー: {request.error}";
                        sepLog.Logger.LogError(error);
                        throw new ReleaseServiceException(error);
                    }

                    sepLog.Logger.LogInfo("リリース情報取得完了", _enableLogger);
                    sepLog.Logger.LogInfo("生データ: " + request.downloadHandler.text, _enableLogger);

                    var releases = JsonConvert.DeserializeObject<List<Release>>(request.downloadHandler.text);

                    LogReleaseInfo(releases);

                    return releases ?? new List<Release>();
                }
                catch (Exception e) when (!(e is ReleaseServiceException))
                {
                    var error = $"リリース情報取得中に予期しないエラーが発生: {e.Message}";
                    sepLog.Logger.LogError(error);
                    throw new ReleaseServiceException(error, e);
                }
            }
        }

        private void LogReleaseInfo(List<Release> releases)
        {
            if (!_enableLogger) return;

            foreach (var release in releases)
            {
                sepLog.Logger.LogInfo($"Release: ID: {release.ID}, Name: {release.Name}, Tag: {release.TagName}, Published At: {release.PublishedAt}", _enableLogger);
                foreach (var asset in release.Assets)
                {
                    sepLog.Logger.LogInfo($"Asset: ID: {asset.ID}, Name: {asset.Name}, URL: {asset.URL}, Size: {asset.Size}", _enableLogger);
                }
            }
        }
    }
}