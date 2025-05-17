using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class AssetsImporter
{
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _defaultToken;

    private const string GasURL =
        "https://script.google.com/macros/s/AKfycbyq3HvEVdpSucXgKPn6gNuLmn231XwVaLHf2-lzseDHCrEb2HhwGNQLV9nWAf0z0ge4/exec";

    public AssetsImporter()
    {
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

    public async Task GetReleases(string route, CancellationToken token = default)
    {
        var ct = token == CancellationToken.None ? _defaultToken : token;
        var req = UnityWebRequest.Get($"{GasURL}?route={route}");
        await req.SendWebRequest().ToUniTask(cancellationToken: ct);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Releases Get Error: " + req.error);
        }

        Debug.Log(req.downloadHandler.text);
    }

    public async Task GetAssetUrl(string route, string assetId, CancellationToken token = default)
    {
        var ct = token == CancellationToken.None ? _defaultToken : token;
        var req = UnityWebRequest.Get($"{GasURL}?route={route}&id={assetId}");
        await req.SendWebRequest().ToUniTask(cancellationToken: ct);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Releases Get Error: " + req.error);
        }

        Debug.Log(req.downloadHandler.text);
    }

    public async UniTask<string> StartFetching()
    {
        var ct = _cts.Token;
        using (var request = UnityWebRequest.Get(GasURL))
        {
            try
            {
                var asyncOp = await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                if (request.result is UnityWebRequest.Result.ConnectionError
                    or UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"ダウンロードエラー: {request.error}");
                    return "";
                }

                Debug.Log(request.result);
            }
            catch (Exception e)
            {
                Debug.LogError($"アセットダウンロードエラー: {e}");
                throw;
            }

            try
            {
                var fileData = request.downloadHandler.data;

                var zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Unity.zip");
                Debug.Log($"zipPath: {zipPath}");
                await File.WriteAllBytesAsync(zipPath, fileData, ct);

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Unity");
                Debug.Log($"filePath: {filePath}");
                await ExtractZipFile(zipPath, filePath, ct);

                return filePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e}");
                return "";
            }
        }

        Debug.LogError("URLの取得に失敗しました");
        return "";
    }

    public async UniTask<string> FetchURLAsync(string apiUrl, CancellationToken ct)
    {
        using (var request = UnityWebRequest.Get(apiUrl))
        {
            var asyncOp = await request.SendWebRequest().ToUniTask(cancellationToken: ct);

            try
            {
                if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("ダウンロードURL取得エラー: " + request.error);
                }


                return request.downloadHandler.text;

                // // JSON文字列を取得
                // var json = request.downloadHandler.text;
                //
                // // JSONをデシリアライズ
                // var responseData = JsonUtility.FromJson<ResponseData>(json);
                //
                // if (responseData is { _items: not null })
                // {
                //     // 整形して出力
                //     foreach (var item in responseData._items)
                //     {
                //         Debug.Log($"Item: {item}");
                //     }
                // }
                // else
                // {
                //     Debug.LogError("JSONのデシリアライズに失敗しました");
                // }
                //
                // return "";
            }
            catch (Exception e)
            {
                Debug.LogError($"RequestError: {request.error}\n Exception: {e}");
                return null;
            }
        }
    }


    /// <summary>
    /// ファイルの解凍処理
    /// </summary>
    /// <param name="zipPath">zipファイルのPath</param>
    /// <param name="filePath">解凍先のフォルダのPath</param>
    /// <param name="ct">基本的にはシーン再生時にキャンセルされる</param>
    private async Task ExtractZipFile(string zipPath, string filePath, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(filePath);
            // ZIPファイルを解凍
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, filePath), ct);

            Debug.Log($"ZIPファイルを解凍しました: {zipPath} -> {filePath}");
        }
        catch (IOException e)
        {
            Debug.LogError($"解凍中にエラーが発生しました: {e.Message}");
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath); // ZIPファイルを削除
                // File.Delete(zipPath + ".meta"); // metaも削除
            }
            else if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            else
            {
                Debug.Log("All Files Deleted");
            }
        }
    }

    // [ContextMenu("Hoge")]
    // public void Hoge()
    // {
    //     Debug.Log("ぼたんわよ");
    //     StartFetching().Forget();
    // }

    // TODO: キャンセル処理はいずれどうにかしたい
    // private void Start()
    // {
    //     // アセットダウンロード処理中にエディターを再生し始めた場合、処理をキャンセルする
    //     _cts.Cancel();
    // }
}


[Serializable]
public struct Release
{
    public int _id;
    public string _name;
    public string _tagName;
    public string _publishedAt;
    public List<Asset> _assets;
}

[Serializable]
public struct Asset
{
    public int _id;
    public string _name;
    public string _url;
    public long _size;
}

// データサンプル
// { id: 219025835,
//     name: 'Release main',
//     tag_name: 'main',
//     published_at: '2025-05-16T07:52:08Z',
//     assets: 
//     [ { id: 255181855,
//         name: 'Unity.zip',
//         download_url: 'https://github.com/RyuichiroYoshida/SepDriveActions/releases/download/main/Unity.zip',
//         size: 63003695 } ] }