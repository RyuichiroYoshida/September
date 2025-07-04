# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

「September」は Unity エンジンを使用したマルチプレイヤーゲームプロジェクトです。Photon Fusion をネットワーキングライブラリとして使用し、リアルタイムマルチプレイヤー機能を実現しています。

## 主要技術スタック

- **Unity Engine**: ゲームエンジン
- **Photon Fusion**: リアルタイムネットワーキング
- **UniTask**: 非同期処理ライブラリ
- **DOTween**: アニメーションライブラリ
- **Universal Render Pipeline (URP)**: レンダリングパイプライン
- **NaughtyAttributes**: Inspector 拡張ライブラリ
- **Cinemachine**: カメラ制御
- **CRI**: 音響システム

## アーキテクチャ構造

### コアシステム

#### 1. ネットワーキング層 (`Assets/Scripts/Common/`)
- `NetworkManager.cs`: ネットワーク接続・セッション管理の中心
- `PhotonSpawner.cs`: オブジェクトの生成・削除を管理
- `PlayerDatabase.cs`: プレイヤー情報の管理
- `SessionPlayerData.cs`: セッション内プレイヤーデータ

#### 2. ゲーム管理層 (`Assets/Scripts/InGame/`)
- `InGameManager.cs`: ゲーム全体の状態管理
- `PlayerManager.cs`: プレイヤーオブジェクトの管理
- `UIController.cs`: UI 全体の制御

#### 3. プレイヤーシステム (`Assets/Scripts/InGame/Player/`)
- `PlayerMovement.cs`: プレイヤーの移動制御
- `PlayerHealth.cs`: プレイヤーの体力システム
- `PlayerStatus.cs`: プレイヤーの状態管理

#### 4. アビリティシステム (`Assets/Scripts/InGame/Player/Ability/`)
- `AbilityBase.cs`: アビリティの基底クラス
- `AbilityExecutor.cs`: アビリティ実行制御
- `AbilityInputHandler.cs`: アビリティ入力処理

#### 5. UI システム (`Assets/Scripts/InGame/UI/`)
- `AlphaUI/`: ゲーム内UI コンポーネント
- `PlayerHpBarManager.cs`: プレイヤーHP表示
- `NoticeManager.cs`: 通知システム

### シーン構成

- **Title**: タイトル画面
- **Lobby**: ロビー画面（マルチプレイヤー待機）
- **InGame**: メイン ゲームプレイ

## 開発ワークフロー

### Unity 固有のワークフロー
Unityプロジェクトのため、主要な開発作業はUnity Editorで行います：

1. **Unity Editor でのビルド**: File → Build Settings からビルド
2. **Play Mode でのテスト**: Unity Editor の Play ボタンでテスト実行
3. **Inspector でのパラメータ調整**: ScriptableObject を通じた設定管理

### アセットインポート
- カスタムアセットインポーター (`Assets/Scripts/Editor/AssetImporter/`)
- `AssetsImporter.cs` を通じた外部アセット取得機能

### テスト環境
- テスト用シーン: `Assets/Scenes/NetworkMock/`
- アビリティテスト: `Assets/Scenes/NetworkMock/AbilityTest.unity`
- ネットワークモック: `Assets/Scenes/NetworkMock/InGameMock.unity`

## 重要な設計パターン

### サービスロケーターパターン
```csharp
ServiceLocator.Instance.Register<IService>(serviceInstance);
var service = ServiceLocator.Instance.Get<IService>();
```

### ネットワークオブジェクトの同期
```csharp
[Networked] public float NetworkedProperty { get; set; }
```

### アビリティシステムの拡張
新しいアビリティを追加する場合：
1. `AbilityBase` を継承
2. `AbilityName` enum に追加
3. `AbilityExecutor` に登録

### UI アニメーション
DOTween を使用したアニメーション：
```csharp
transform.DOMove(targetPosition, duration);
```

## 主要設定ファイル

- `Assets/ScriptableObjects/CharacterDataContainer.asset`: キャラクター設定
- `Assets/ScriptableObjects/SpawnablePrefabDatabase.asset`: 生成可能オブジェクト管理
- `Assets/Settings/`: レンダリングパイプライン設定

## 注意事項

### ネットワーク同期
- プレイヤーの状態変更は必ず `[Networked]` プロパティ経由で行う
- RPC 呼び出しには適切な `RpcTargets` を指定

### パフォーマンス
- UniTask を使用した非同期処理でメインスレッドをブロックしない
- オブジェクトプールでガベージコレクションを最小化

### デバッグ
- `Assets/Prefabs/Debug/` にデバッグ用プレハブを配置
- シーン `DevStartInGame.unity` で開発テスト可能