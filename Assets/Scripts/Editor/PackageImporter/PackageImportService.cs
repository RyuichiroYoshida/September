using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace September.Editor.PackageImporter
{
    public static class UnityWebRequestAwaiterExtensions
    {
        public static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            
        }
    }

    public static class PackageImportService
    {
        private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";

        private static async Task<List<DriveFileEntry>> ListPackagesAsync(string folderId, string apiKey)
        {
            var result = new List<DriveFileEntry>();
            string pageToken = null;

            do
            {
                var query = $"'{folderId}' in parents and trashed = false and name contains '.unitypackage'";
                var url = $"{FilesEndpoint}?q={Uri.EscapeDataString(query)}" + 
                          $"&fields={Uri.EscapeDataString("files(id,name,modifiedTime,size),nextPageToken")}" + 
                          $"&pageSize=100&key={apiKey}";
                
                if (!string.IsNullOrEmpty(pageToken))
                    url += $"pageToken={Uri.EscapeDataString(pageToken)}";
                
                using (var request = UnityWebRequestAwaiterExtensions.GetAwaiter(url))
                {
                    await request.SendWebRequest();

                    if (request.result != UnityWebRequestAwaiterExtensions.Result.Success)
                    {
                        throw new Exception(
                            $"Drive一覧取得に失敗しました ({request.responseCode}): {request.error}\n" + 
                            $"{request.downloadHandler?.text}");
                    }

                    var response = JsonUtility.FromJson<DriveFileResponse>(request.downloadHandler.text);
                    if (response?.files != null) 
                        result.AddRange(response.files);

                    pageToken = response?.nextPageToken;
                }
            } while (!string.IsNullOrEmpty(pageToken));

            return result;
        }

        public static async Task DownloadFileAsync(string fileId, string apiKey, string destinationPath)
        {
            var url = $"{FilesEndpoint}/{fileId}?alt=media&key={apiKey}";

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception(
                        $"ダウンロードに失敗しました ({request.responseCode}): {request.error}");
                }
            }
        }
    }
}