using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;
using sepLog = September.Editor.Logger;

public struct ProgressInfo
{
    public string Status { get; set; }
    public float Progress { get; set; }
    public string Detail { get; set; }
}

public class AssetsImporter
{
    private const string ApiUrl = "https://asset-importer-538394701382.asia-northeast1.run.app";
    
    private readonly bool _enableLogger;

    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _defaultToken;

    public List<Release> Releases { get; private set; } = new();
    
    public event Action<ProgressInfo> OnProgressChanged;

    public AssetsImporter(bool enableLogger = true)
    {
        _enableLogger = enableLogger;
        _cts = new CancellationTokenSource();
        _defaultToken = _cts.Token;
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }
    }

    public async UniTask<bool> GetReleases(string route, CancellationToken? token = null)
    {
        var ct = token ?? _defaultToken;
        var req = UnityWebRequest.Get($"{ApiUrl}/{route}");
        try
        {
            await req.SendWebRequest().ToUniTask(cancellationToken: ct);
        }
        catch (Exception e)
        {
            sepLog.Logger.LogError("Releases Get Error: " + e.Message);
            return false;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            sepLog.Logger.LogError("Releases Get Error: " + req.error);
            return false;
        }

        sepLog.Logger.LogInfo("生データ: " + req.downloadHandler.text, _enableLogger);

        try
        {
            Releases = JsonConvert.DeserializeObject<List<Release>>(req.downloadHandler.text);
            foreach (var release in Releases)
            {
                sepLog.Logger.LogInfo($"Release: ID: {release.ID}, Name: {release.Name}, Tag: {release.TagName}, Published At: {release.PublishedAt}", _enableLogger);
                foreach (var asset in release.Assets)
                {
                    sepLog.Logger.LogInfo($"Asset: ID: {asset.ID}, Name: {asset.Name}, URL: {asset.URL}, Size: {asset.Size}", _enableLogger);
                }
            }
        }
        catch (Exception e)
        {
            sepLog.Logger.LogError($"JSONパースエラー: {e.Message}");
            return false;
        }

        return true;
    }

    public async UniTask<string> GetAsset(string route, int assetId, CancellationToken? token = null)
    {
        var ct = token ?? _defaultToken;
        var urlReq = UnityWebRequest.Get($"{ApiUrl}/{route}?id={assetId}");
        
        OnProgressChanged?.Invoke(new ProgressInfo 
        { 
            Status = "ダウンロード開始", 
            Progress = 0f, 
            Detail = "アセットのダウンロードを開始しています..." 
        });
        
        try
        {
            var operation = urlReq.SendWebRequest();
            
            while (!operation.isDone)
            {
                var progress = urlReq.downloadProgress;
                OnProgressChanged?.Invoke(new ProgressInfo 
                { 
                    Status = "ダウンロード中", 
                    Progress = progress, 
                    Detail = $"ダウンロード進捗: {progress * 100:F1}%" 
                });
                
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            
            await operation.ToUniTask(cancellationToken: ct);
        }
        catch (Exception e)
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "エラー", 
                Progress = 0f, 
                Detail = "ダウンロードエラー: " + e.Message 
            });
            sepLog.Logger.LogError("GetAsset Error: " + e.Message);
            return "";
        }

        if (urlReq.result != UnityWebRequest.Result.Success)
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "エラー", 
                Progress = 0f, 
                Detail = "ダウンロードエラー: " + urlReq.error 
            });
            sepLog.Logger.LogError("Url Get Error: " + urlReq.error);
            return "";
        }
        
        try
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "ファイル保存中", 
                Progress = 1f, 
                Detail = "ダウンロードしたファイルを保存しています..." 
            });
            
            var fileData = urlReq.downloadHandler.data;

            // アセット名を取得
            var assetName = Releases
                .SelectMany(r => r.Assets)
                .FirstOrDefault(a => a.ID == assetId).Name ?? "Asset.zip";
            // 先頭の \ や / を除去
            assetName = assetName.Replace("\\", "").Replace("/", "");

            // zipファイルのパス
            var zipPath = Path.Combine(Directory.GetCurrentDirectory(), assetName);

            // ディレクトリ名も同様に先頭の \ や / を除去
            var fileDirName = Path.GetFileNameWithoutExtension(assetName).TrimStart('\\', '/');
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileDirName);
            
            sepLog.Logger.LogInfo($"Zip Path: {zipPath}", _enableLogger);
            await File.WriteAllBytesAsync(zipPath, fileData, ct);
            
            sepLog.Logger.LogInfo($"ファイルの保存先: {filePath}", _enableLogger);
            await ExtractZipFile(zipPath, filePath, ct);

            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "完了", 
                Progress = 1f, 
                Detail = "アセットのダウンロードと解凍が完了しました" 
            });

            return filePath;
        }
        catch (Exception e)
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "エラー", 
                Progress = 0f, 
                Detail = "ファイル保存エラー: " + e.Message 
            });
            sepLog.Logger.LogError($"ファイルの保存中にエラーが発生しました: {e.Message}");
            return "";
        }
    }

    /// <summary>
    /// ファイルの解凍処理
    /// </summary>
    /// <param name="zipPath">zipファイルのPath</param>
    /// <param name="filePath">解凍先のフォルダのPath</param>
    /// <param name="ct">基本的にはシーン再生時にキャンセルされる</param>
    private async UniTask ExtractZipFile(string zipPath, string filePath, CancellationToken ct)
    {
        try
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "解凍開始", 
                Progress = 0f, 
                Detail = "ZIPファイルの解凍を開始しています..." 
            });
            
            Directory.CreateDirectory(filePath);
            
            // ZIPファイルの総エントリ数を取得して進捗を監視
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var totalEntries = archive.Entries.Count;
                var extractedEntries = 0;
                
                foreach (var entry in archive.Entries)
                {
                    if (ct.IsCancellationRequested)
                        return;
                        
                    var destinationPath = Path.Combine(filePath, entry.FullName);
                    
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true);
                    }
                    
                    extractedEntries++;
                    var progress = (float)extractedEntries / totalEntries;
                    
                    OnProgressChanged?.Invoke(new ProgressInfo 
                    { 
                        Status = "解凍中", 
                        Progress = progress, 
                        Detail = $"解凍進捗: {extractedEntries}/{totalEntries} ({progress * 100:F1}%)" 
                    });
                }
            }, ct);

            sepLog.Logger.LogInfo($"ZIPファイルを解凍しました: {zipPath} -> {filePath}", _enableLogger);
        }
        catch (IOException e)
        {
            OnProgressChanged?.Invoke(new ProgressInfo 
            { 
                Status = "エラー", 
                Progress = 0f, 
                Detail = "解凍エラー: " + e.Message 
            });
            sepLog.Logger.LogError($"解凍中にIOエラーが発生しました: {e.Message}");
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath); // ZIPファイルを削除
                // File.Delete(zipPath + ".meta"); // metaも削除
            }
            else if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, true); // Delete directory and its contents
            }
            else if (File.Exists(filePath))
            {
                File.Delete(filePath); // Delete file
            }
            else
            {
                sepLog.Logger.LogError("All Files Deleted");
            }
        }
    }
}

[Serializable]
public struct Release
{
    [JsonProperty("id")] public int ID { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("tag_name")] public string TagName { get; set; }
    [JsonProperty("published_at")] public string PublishedAt { get; set; }
    [JsonProperty("assets")] public List<Asset> Assets { get; set; }
}

[Serializable]
public struct Asset
{
    [JsonProperty("id")] public int ID { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("download_url")] public string URL { get; set; }
    [JsonProperty("size")] public int Size { get; set; }
}

// データサンプル
// {
//     [
//         id: 219025835,
//         name: 'Release main',
//         tag_name: 'main',
//         published_at: '2025-05-16T07:52:08Z',
//         assets: 
//             [
//                 {
//                     id: 255181855,
//                     name: 'Unity.zip',
//                     download_url: 'https://github.com/RyuichiroYoshida/SepDriveActions/releases/download/main/Unity.zip',
//                     size: 63003695
//                 },
//             ]
//     ],
// }