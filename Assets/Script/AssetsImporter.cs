using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class AssetsImporter : MonoBehaviour
{
    private string _downloadUrl;
    private CancellationTokenSource _cts = new();

    private async UniTask<string> FetchURLAsync(string apiUrl, CancellationToken ct)
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

                return request.downloadHandler.text; // 取得したURLを返す
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {request.error}");
                return null;
            }
        }
    }

    private async UniTaskVoid StartFetching()
    {
        // TODO: 実行時間が1分くらいかかるので、APIを軽量化したい
        // Google Apps ScriptのURL
        const string gasUrl =
            "https://script.google.com/macros/s/AKfycbyhfhBWdQgnGO0RQnuLfAdzoE1_wBcS5szmdhnvMNH8J5kK59g3alaLH_dXFkjUkQ7f/exec";
        
        var ct = _cts.Token;
        
        // URl取得のテストは完了したため、コメントアウト
        // _downloadUrl = await FetchURLAsync(gasUrl, ct);
        _downloadUrl = "https://drive.google.com/file/d/1bkGoNGPCgynxa_QcH9LyjEbC1Y3_bgTA/view?usp=drive_link";
        
        if (!string.IsNullOrEmpty(_downloadUrl))
        {
            // URLを使用した後続処理

            using (var request = UnityWebRequest.Get(_downloadUrl))
            {
                var asyncOp = await request.SendWebRequest().ToUniTask(cancellationToken: ct);

                try
                {
                    if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError($"ダウンロードエラー: {request.error}");
                        return;
                    }
                    
                    var fileData = request.downloadHandler.data;
                    // 保存先
                    var zipPath = Application.dataPath + "/AssetStoreTools/Unity.zip"; 
                    
                    await File.WriteAllBytesAsync(zipPath, fileData, ct);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error: {e}");
                }
                
                
                var path = Path.Combine(Application.dataPath, "AssetStoreTools", "Unity.zip");
                Debug.Log(path);
                //ExtractZipFile(@"C:\Users\vantan\Downloads\Unity.zip");
                ExtractZipFile(Directory.GetCurrentDirectory() + @"\Unity.zip");
            }
        }
        else
        {
            Debug.LogError("URLの取得に失敗しました");
        }
    }
    
    private void ExtractZipFile(string zipPath)
    {
        try
        {
            // 解凍先のpath
            //var filePath = Path.Combine(Application.dataPath, "AssetStoreTools", "test");
            //var filePath = @"C:\Users\vantan\Downloads\Unity";
            var filePath = Directory.GetCurrentDirectory() + @"\Unity";

            // ZIPファイルを解凍
            ZipFile.ExtractToDirectory(zipPath, filePath);
            Debug.Log($"ZIPファイルを解凍しました: {zipPath} -> {filePath}");
            
            // ZIPファイルを開いてZipArchiveオブジェクトを作る
            // using (var archive = ZipFile.OpenRead(zipPath))
            // {
            //     // 選択したファイルを指定したフォルダーに書き出す
            //     foreach (var entry in archive.Entries)
            //     {
            //         // ZipArchiveEntryオブジェクトのExtractToFileメソッドにフルパスを渡す
            //         var destinationPath = Path.Combine(filePath, entry.FullName);
            //         Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);
            //         entry.ExtractToFile(destinationPath, overwrite: true);
            //     }
            // }
            
        }
        catch (IOException e)
        {
            Debug.LogError($"解凍中にエラーが発生しました: {e.Message}");
        }
        // finally
        // {
        //     if (File.Exists(zipPath))
        //     {
        //         File.Delete(zipPath); // ZIPファイルを削除
        //         File.Delete(zipPath + ".meta"); // metaも削除
        //     }
        // }
    }
    
    [ContextMenu("Hoge")]
    public void Hoge()
    {
        Debug.Log("ぼたんわよ");
        StartFetching().Forget();
    }

    private void Start()
    {
        // アセットダウンロード処理中にエディターを再生し始めた場合、処理をキャンセルする
        //_cts.Cancel();
    }
}