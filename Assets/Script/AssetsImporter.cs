using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class AssetsImporter
{
    private string _downloadUrl;
    private CancellationTokenSource _cts = new();

    public async Task Test()
    {
        await DownloadFileAsync("1NMt0nEGOkyPJ1kCX4xzgyFfba9kkxxPm", "Unity.zip");
    }

    private static readonly HttpClientHandler Handler = new()
    {
        CookieContainer = new CookieContainer(),
        AllowAutoRedirect = true,
        UseCookies = true
    };

    private static readonly HttpClient Client = new(Handler);

    public static async Task DownloadFileAsync(string fileId, string outputPath)
    {
        var baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

        // Step 1: 最初のリクエスト（ウイルススキャンページ）
        var response = await Client.GetAsync(baseUrl);
        var content = await response.Content.ReadAsStringAsync();

        // Step 2: confirm トークンを正規表現で抽出
        var match = Regex.Match(content, @"confirm=([0-9A-Za-z_]+)");
        var confirmToken = match.Success ? match.Groups[1].Value : "t";

        // Step 3: confirm トークン付きで再リクエスト
        var downloadUrl = $"https://drive.google.com/uc?export=download&confirm={confirmToken}&id={fileId}";
        var fileResponse = await Client.GetAsync(downloadUrl);

        fileResponse.EnsureSuccessStatusCode();

        using (var fs = new FileStream(outputPath, FileMode.Create))
        {
            await fileResponse.Content.CopyToAsync(fs);
        }

        Debug.Log("File downloaded successfully.");
    }

    // public async Task Test()
    // {
    //     const string fileId = "1NMt0nEGOkyPJ1kCX4xzgyFfba9kkxxPm";
    //     var url = $"https://drive.google.com/uc?export=download&id={fileId}";
    //         
    //     var req = UnityWebRequest.Get(url);
    //     req.downloadHandler = new DownloadHandlerBuffer();
    //     await req.SendWebRequest();
    //     
    //     if (req.result != UnityWebRequest.Result.Success)
    //     {
    //         Debug.LogError("Initial request failed: " + req.error);
    //         return;
    //     }
    //     
    //     // Step 2: confirm=t が含まれている場合（Virus Scan確認）
    //     var containsConfirmT = req.downloadHandler.text.Contains("name=\"confirm\" value=\"t\"");
    //     var confirmToken = containsConfirmT ? "t" : "";
    //
    //     // Step 3: 再ダウンロード（正しいファイル本体取得）
    //     var finalUrl = string.IsNullOrEmpty(confirmToken)
    //         ? url
    //         : $"https://drive.google.com/uc?export=download&confirm={confirmToken}&id={fileId}";
    //
    //     var zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Unity");
    //     var downloadReq = UnityWebRequest.Get(finalUrl);
    //     downloadReq.downloadHandler = new DownloadHandlerFile(zipPath);
    //     await downloadReq.SendWebRequest();
    // }

    public async UniTask<string> StartFetching()
    {
        // TODO: 実行時間が1分くらいかかるので、APIを軽量化したい
        // Google Apps ScriptのURL
        const string gasUrl =
            "https://script.google.com/macros/s/AKfycbyhfhBWdQgnGO0RQnuLfAdzoE1_wBcS5szmdhnvMNH8J5kK59g3alaLH_dXFkjUkQ7f/exec";

        var ct = _cts.Token;

        //_downloadUrl = await FetchURLAsync(gasUrl, ct);
        _downloadUrl = "https://drive.google.com/uc?export=download&id=1oh_5onnMxn5-A3rweZnBkqVdjyo7ybqI";

        if (!string.IsNullOrEmpty(_downloadUrl))
        {
            // URLを使用した後続処理

            using (var request = UnityWebRequest.Get(_downloadUrl))
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