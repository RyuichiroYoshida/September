using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using sepLog = September.Editor.Logger;

namespace September.Editor.AssetImporter
{
    public class AssetImportController : IAssetImportController, IDisposable
    {
        private IAssetImportService _assetImportService;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private List<Release> _releases = new();
        private bool _lastUseLoggerSetting;
        
        public bool IsImporting { get; private set; }
        
        public List<string> ReleaseNames => _releases.Select(r => r.TagName).ToList();
        
        public int SelectedReleaseIndex 
        { 
            get => AssetImportSettings.SelectedReleaseIndex;
            set => AssetImportSettings.SelectedReleaseIndex = value;
        }

        public event Action<ProgressInfo> OnProgressChanged;
        public event Action<string> OnStatusChanged;

        public AssetImportController(IAssetImportService assetImportService = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _lastUseLoggerSetting = AssetImportSettings.UseLogger;
            
            _assetImportService = assetImportService ?? CreateAssetImportService();
            SubscribeToServiceEvents();
        }

        private IAssetImportService CreateAssetImportService()
        {
            return new AssetImportService(enableLogger: AssetImportSettings.UseLogger);
        }

        private void SubscribeToServiceEvents()
        {
            if (_assetImportService != null)
            {
                _assetImportService.OnProgressChanged += HandleProgressChanged;
            }
        }

        private void UnsubscribeFromServiceEvents()
        {
            if (_assetImportService != null)
            {
                _assetImportService.OnProgressChanged -= HandleProgressChanged;
            }
        }

        private void HandleProgressChanged(ProgressInfo progressInfo)
        {
            OnProgressChanged?.Invoke(progressInfo);
        }

        private void CheckAndUpdateSettings()
        {
            // ログ設定が変更された場合、サービスを再作成
            if (_lastUseLoggerSetting != AssetImportSettings.UseLogger)
            {
                _lastUseLoggerSetting = AssetImportSettings.UseLogger;
                RecreateAssetImportService();
            }
        }

        private void RecreateAssetImportService()
        {
            UnsubscribeFromServiceEvents();
            _assetImportService = CreateAssetImportService();
            SubscribeToServiceEvents();
        }

        public async UniTask InitializeAsync()
        {
            try
            {
                CheckAndUpdateSettings();
                OnStatusChanged?.Invoke(AssetImportConstants.Messages.LoadingResources);
                
                _releases = await _assetImportService.GetReleasesAsync(
                    AssetImportConstants.Routes.Releases, 
                    _cancellationTokenSource.Token);
                
                // 前回選択されたインデックスが範囲外の場合は0にリセット
                if (SelectedReleaseIndex >= _releases.Count)
                {
                    SelectedReleaseIndex = 0;
                }
                
                OnStatusChanged?.Invoke(AssetImportConstants.Messages.ResourcesLoaded);
            }
            catch (Exception e)
            {
                sepLog.Logger.LogError($"初期化エラー: {e.Message}");
                OnStatusChanged?.Invoke($"初期化に失敗しました: {e.Message}");
                throw;
            }
        }

        public async UniTask ImportSelectedAssetAsync()
        {
            if (IsImporting) return;
            
            try
            {
                CheckAndUpdateSettings();
                IsImporting = true;
                OnStatusChanged?.Invoke(AssetImportConstants.Messages.ImportInProgress);
                
                if (_releases.Count == 0 || SelectedReleaseIndex >= _releases.Count)
                {
                    throw new AssetImportException("有効なリリースが選択されていません");
                }

                var selectedRelease = _releases[SelectedReleaseIndex];
                if (selectedRelease.Assets == null || selectedRelease.Assets.Count == 0)
                {
                    throw new AssetImportException("選択されたリリースにアセットが含まれていません");
                }

                var assetId = selectedRelease.Assets[0].ID;
                
                var extractPath = await _assetImportService.DownloadAndExtractAssetAsync(
                    AssetImportConstants.Routes.Asset, 
                    assetId, 
                    _cancellationTokenSource.Token);
                
                _assetImportService.ImportUnityPackages(extractPath, AssetImportSettings.ShowImportWindow);
                
                OnStatusChanged?.Invoke(AssetImportConstants.Messages.ImportCompleted);
            }
            catch (Exception e)
            {
                sepLog.Logger.LogError($"インポートエラー: {e.Message}");
                OnStatusChanged?.Invoke(AssetImportConstants.Messages.ImportFailed);
                throw;
            }
            finally
            {
                IsImporting = false;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            // イベント購読解除
            UnsubscribeFromServiceEvents();
        }
    }
}