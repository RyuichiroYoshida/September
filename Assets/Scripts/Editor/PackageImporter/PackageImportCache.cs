using System.IO;
using UnityEngine;

namespace September.Editor.PackageImporter
{
    public static class PackageImportCache
    {
        private static readonly string CacheDir = Path.Combine("Library", "PackageImporter");
        private static readonly string CacheFilePath = Path.Combine(CacheDir, "cache.json");
        public static readonly string DownloadDir = Path.Combine(CacheDir, "Downloads");

        public static CacheData Load()
        {
            EnsureDirs();
            if (!File.Exists(CacheFilePath))
                return new CacheData();

            try
            {
                var json = File.ReadAllText(CacheFilePath);
                var data = JsonUtility.FromJson<CacheData>(json);
                return data ?? new CacheData();
            }
            catch
            {
                return new CacheData();
            }
        }

        public static void Save(CacheData data)
        {
            EnsureDirs();
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(CacheFilePath, json);
        }
        
        private static void EnsureDirs()
        {
            if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);
            if (!Directory.Exists(DownloadDir)) Directory.CreateDirectory(DownloadDir);
        }
    }
}