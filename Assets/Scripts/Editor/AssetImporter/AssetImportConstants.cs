namespace September.Editor.AssetImporter
{
    public static class AssetImportConstants
    {
        public const string ApiUrl = "https://asset-importer-538394701382.asia-northeast1.run.app";
        public const string WindowTitle = "Asset Import Tool";
        public const string MenuPath = "September/Import";
        
        public const int ProgressUpdateIntervalMs = 100;
        public const int DefaultTimeoutMs = 120000;
        
        public static class Routes
        {
            public const string Releases = "releases";
            public const string Asset = "asset";
        }
        
        public static class Messages
        {
            public const string LoadingResources = "リソース読み込み中";
            public const string ImportWaiting = "インポート実行待機中";
            public const string ImportInProgress = "アセットインポート中";
            public const string ImportCompleted = "アセットのインポートが完了しました";
            public const string ImportFailed = "アセットのインポートに失敗しました";
            public const string ResourcesLoaded = "リソース読み込みが完了しました";
            public const string DoNotPlayScene = "インポートが終わるまでUnityのシーンを再生しないでください！";
            public const string Initializing = "初期化中...";
            public const string ProgrammerSection = "-----------------これより下はプログラマー用-----------------";
        }
        
        public static class ProgressMessages
        {
            public const string DownloadStarting = "ダウンロード開始";
            public const string Downloading = "ダウンロード中";
            public const string FileSaving = "ファイル保存中";
            public const string ExtractionStarting = "解凍開始";
            public const string Extracting = "解凍中";
            public const string Importing = "インポート中";
            public const string Completed = "完了";
            public const string Error = "エラー";
            
            public const string DownloadStartDetail = "アセットのダウンロードを開始しています...";
            public const string FileSaveDetail = "ダウンロードしたファイルを保存しています...";
            public const string ExtractionStartDetail = "ZIPファイルの解凍を開始しています...";
            public const string ImportDetail = "UnityPackageをインポートしています...";
            public const string CompletedDetail = "アセットのダウンロードと解凍が完了しました";
        }
        
        public static class FileExtensions
        {
            public const string UnityPackage = "*.unitypackage";
            public const string Zip = ".zip";
        }
    }
}