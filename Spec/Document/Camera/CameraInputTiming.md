# カメラの入力タイミングと読み取りタイミング

作成日: 2026-09-06  対象: develop (#785 マージ後) + PR #784 の内容  関連 PR: #784 (旧改修), 本文書を含む PR

## 0. 結論

- **ズレは 2 種類あり、原因は別。** #784 が直したのは「入力に載る CameraYaw が 1 フレーム古い」だけで、どちらの本体も直していない。
  - **1 フレームのズレ**: `CinemachineBrain.LateUpdate` (Camera.main へ書く側) と、カメラを回す `PlayerManager.LateUpdate` の実行順が未定義。シーンに元からある Brain は後から Spawn されたプレイヤーより先に LateUpdate されるため、Brain は 1 フレーム前の姿勢を写す。
  - **1 ティックのズレ**: `CameraPivot` はプレイヤー Root (最新 Tick の位置) の子だが、見えているメッシュは `NetworkRigidbody3D` の補間ターゲット (`Mesh`) で「前 Tick と最新 Tick の間」に描かれる。カメラは常にキャラクターより最大 1 Tick 先行し、その量が毎フレーム変わる。
- **#784 で悪化した理由**: 回転の適用箇所が「Tick が回るフレームは `OnInput` (Update 段)、回らないフレームは `LateUpdate`」に分かれた。Brain が先に走る環境では、前者は当フレーム、後者は 1 フレーム遅れで写るため、遅れが一定ではなくフレームごとに交互になり、揺れとして目立つ。
- **修正 (本 PR)**: (1) カメラリグの LateUpdate を Brain より前の実行順に固定し、視点入力の適用をリグ側へ移す。(2) Pivot 位置を補間ターゲット基準に置き直す。(3) カメラリセット / オフセットの Tween を Update 段へ移し、リグの LateUpdate より前に確定させる。

## 1. 前提 (リポジトリで確認した事実)

| 項目 | 値 | 根拠 |
|---|---|---|
| CameraPivot の親 | プレイヤー Root (`_characterTf`) | `PlayerBase.prefab` Transform `m_Father` |
| NetworkRigidbody3D の補間ターゲット | `Mesh` (Root の子、Animator 持ち) | `PlayerBase.prefab` `_interpolationTarget` (2026-07-12 から) |
| Rigidbody.interpolation | None | `m_Interpolate: 0` |
| VirtualCamera | `PlayerVirtualCamera.prefab` (Cinemachine 3.1.7)。Follow / LookAt 無し。CameraPivot の子 | Prefab |
| CinemachineBrain | `UpdateMethod: SmartUpdate`, `BlendUpdateMethod: LateUpdate`。マップシーン (Pirate_Field / InGameMock) 内に配置 | シーン YAML |
| Brain / リグの Script Execution Order | 設定なし (どちらも既定 0) | `.meta` と ProjectSettings に指定なし |
| Fusion 物理 | `RunnerSimulatePhysics3D` (`ClientPhysicsSimulation: SimulateAlways`, `_physicsTiming: 3`) | `NetworkRunner.prefab` |
| 入力 | InputSystem 1.13.1、更新モード既定 (Dynamic)。`Look` はマウス Delta | 設定アセットなし |

Fusion 2 の Runner 更新 (OnInput → FixedUpdateNetwork → Render) は Unity の Update 段で、通常の MonoBehaviour の Update より先に走る。Fusion 本体はリポジトリ外 (Import) なのでコードでは確認していない。#784 も同じ前提で動作確認されている。

## 2. カメラへの入力 (書き込み) タイミング

書き込み先は `CameraPivot.rotation` (向き)、`CameraPivot.position` (位置、本 PR で追加)、`_cameraTf.position` (障害物回避後のカメラ位置)。

| # | 段 | 呼び出し元 | 書くもの | deltaTime | 備考 |
|---|---|---|---|---|---|
| W1 | Fusion Update (OnInput) | `InputProvider.OnInput` → `ILookInputReceiver.TryApplyLookInput` (#784) | Pivot 向き | `Time.deltaTime` | Tick が回るフレームのみ。フレーム 1 回ガードあり |
| W2 | FixedUpdateNetwork | `PlayerMovement` (Rigidbody 速度 / `transform.position`) → Root が動く | Pivot 位置 (親経由) | Tick | 最新 Tick の位置 |
| W3 | Fusion Render | `NetworkRigidbody3D.Render` → `Mesh` を補間位置へ | (Pivot には届かない) | | 前 Tick 〜 最新 Tick の間 |
| W4 | LateUpdate (順序未定義) | `PlayerManager.LateUpdate` → `TryApplyLookInput` / `CameraReset` | Pivot 向き | `Time.deltaTime` | 旧実装。W1 が先なら何もしない |
| W5 | LateUpdate (順序未定義) | `CameraController.LateUpdate` → `CheckCameraDistance` | `_cameraTf` 位置 | | 障害物回避 |
| W6 | LateUpdate (DOTween Late) | `SmoothRotateCameraTo` Tween (カメラリセット) | Pivot 向き | | リセット中は W1/W4 を `_isInRotation` で無効化 |
| W7 | LateUpdate (DOTween Late) | `ChangeOffset` Tween | `_currentOffset` | | W5 が次に読む |
| W8 | LateUpdate | `MountableExhibitBase.LateUpdate`, `Kraken.LateUpdate` | 展示物リグの Pivot 向き | **`Runner.DeltaTime`** | フレーム毎の呼び出しに Tick 時間を掛けている (感度がフレームレート依存) |
| W9 | Fusion Render | `BallistaMove.Render` → `RotateCamera` + 独自クランプ | バリスタリグの Pivot 向き | `Time.deltaTime` | Brain より前なので順序問題なし |
| W10 | FixedUpdateNetwork | `PlayerManager` のワープ / 展示物 `GetOn` (ホストのみ) → `CameraReset` | Tween 発火 | | ホスト側だけで Tween が走る既知問題 (別件) |
| W11 | LateUpdate (Brain) | `CinemachineBrain.LateUpdate` → `Camera.main` の Transform | Camera.main | | SmartUpdate。Follow 無しなので Late 判定 |

## 3. カメラ姿勢の読み取りタイミング

| # | 段 | 読み手 | 読むもの | 何に使うか |
|---|---|---|---|---|
| R1 | Fusion Update (OnInput) | `InputProvider.OnInput` → `CameraViewResolver` (#784: アクティブ VirtualCamera の Transform。旧: `Camera.main`) | 向き・位置・Yaw | `PlayerInput.CameraYaw / DesiredLookDirection / CameraPosition` |
| R2 | FixedUpdateNetwork | `PlayerMovement.UpdateMovement` | `input.CameraYaw` | 移動方向 |
| R3 | FixedUpdateNetwork | `AimCameraController`, `UltKunai`, `ThrowKunai`, `TakamuraScanner`, `BallistaMove`, `Pterodactyl`, `Shark`, `Tyranno` | `input.DesiredLookDirection / CameraPosition` | 照準・向き |
| R4 | FixedUpdateNetwork | `PlayerManager.RecordLookState` (#784) | input → `[Networked] LookDirection / LookYaw / LookOrigin` | 他クライアントからの参照 |
| R5 | LateUpdate (Brain) | `CinemachineBrain` | VirtualCamera (= Pivot) の Transform | 描画カメラ |
| R6 | LateUpdate / Update (順序未定義) | `KrakenAimPointResolver`, `TanihiraCursor`, `TakamuraScanner`, `AbilityGrapplingHook`, `ThrowKunai`, `PlayerNameUI`, `CanvasRotator`, `InteractUi`, `DamageTextFactory`, `PlayerAudioController` | `Camera.main` | 照準レイ・ビルボード・音 |

## 4. 1 フレーム内の前後関係

```
[InputSystem 更新 (Look の Delta 確定)]
[Fusion Update]
   OnInput ................ W1 (Pivot 回転, #784) → R1 (VirtualCamera を読む)
   FixedUpdateNetwork ..... R2/R3/R4 → W2 (Root 移動)
   Render ................. W3 (Mesh を補間位置へ), W9
[Update]  DOTween Normal
[LateUpdate]  ※ 同じ実行順 0 同士は生成順。Brain (シーン常駐) が先、プレイヤー (Spawn) が後になりやすい
   W11 Brain → Camera.main         ← ここが先だと以下の結果は次フレームまで写らない
   W4  PlayerManager: 回転 (旧)
   W5  CameraController: 障害物回避
   W6/W7 DOTween Late
   W8  展示物 / クラーケン: 回転
   R6  Camera.main 読み
```

### 4.1 旧実装 (#784 前)

- 回転は毎フレーム W4 (LateUpdate)。Brain が先に走る場合、**常に 1 フレーム遅れ**で写る。一定なので気付きにくい。
- R1 は `Camera.main` を読む → 前フレームの Brain 出力。移動方向 (R2) はさらに 1 フレーム古い Yaw を使う。
- 位置: Pivot は Root 追従 (最新 Tick)、Mesh は補間 (最大 1 Tick 遅れ) → **常に最大 1 Tick のズレ**。

### 4.2 #784 適用後

- Tick が回るフレームは W1 で当フレームに回転 (Brain より前)。回らないフレームは W4 (Brain より後になり得る)。→ **遅れが交互に変わる**。
- R1 は VirtualCamera の Transform を直接読むので Yaw の遅れは解消 (これは正しい)。
- 位置のズレ (1 Tick) は手つかず。

### 4.3 本 PR 適用後

- `CameraController` を `DefaultExecutionOrder(-100)` にして、LateUpdate 内で「視点入力の適用 → 補間ターゲット追従 → 障害物回避」を Brain (0) より前に終える。
- Pivot 位置を `NetworkTRSP.InterpolationTarget` (= Mesh) 基準に毎フレーム置き直す (`CameraPivotFollower`)。補間ターゲットが無いリグ (展示物) は従来通り。
- Tween を `UpdateType.Normal` (Update 段) に移し、リグの LateUpdate より前に値が確定するようにする。
- W1 (OnInput 時の回転) は入力 Yaw の鮮度のために維持。W1 と LateUpdate のどちらで回っても Brain より前になるので、フレームごとの差は無くなる。

## 5. 見つかった問題のある処理順 (優先順)

| 優先 | 問題 | 影響 | 本 PR での扱い |
|---|---|---|---|
| 1 | W4/W5 と W11 (Brain) の順序が未定義。実際は Brain が先になりやすい | 回転・障害物回避が 1 フレーム遅れて写る。#784 後は交互に遅れて揺れる | 修正 (実行順固定 + リグ側で適用) |
| 2 | Pivot が Root の子、Mesh が補間ターゲット | 移動中にカメラがキャラクターより最大 1 Tick 先行し毎フレーム変動 | 修正 (`CameraPivotFollower`) |
| 3 | W6/W7 (DOTween Late) が W5 の後になり得る | カメラリセット / オフセット変更中に 1 フレーム遅れ | 修正 (`UpdateType.Normal`) |
| 4 | W8 が `Runner.DeltaTime` を使用 | 展示物・クラーケンの感度がフレームレートに依存。遅れではない | 未対応。`Time.deltaTime` へ統一すべき |
| 5 | W8 も Brain との順序未定義 | 展示物・クラーケン操作中は 1 フレーム遅れが残る | 未対応。`MountableExhibitBase` / `Kraken` の LateUpdate を `CameraRigExecutionOrder.Rig` にするか、`CameraController.SetLocalLookInputEnabled` へ寄せる |
| 6 | R6 の `Camera.main` 読みが LateUpdate 内で Brain との順序未定義 | クラーケン照準マーカー等が 1 フレーム古い姿勢を使うことがある | 未対応。VirtualCamera (Pivot) を読むか、Brain の後に読む |
| 7 | W10 がホストでのみ Tween | 別件 (memory 済み) | 未対応 |

## 6. 実機での確認手順 (本 PR)

1. 60 fps 固定と、Tick レートより高いフレームレート (144 fps 等) の両方で、前進しながらマウスを左右に振る。
   - 期待: キャラクターの足元とカメラの相対位置が移動中に前後にブレない。マウス回転が毎フレーム同じ遅れ (体感ゼロ) で写る。
2. Aim ボタンでカメラリセット。Tween 中にカクつかないこと。
3. 壁際でカメラが壁に押し出される挙動が従来どおりであること。
4. 展示物 (バリスタ / 恐竜 / クラーケン) に乗り降りしてプレイヤーカメラが復帰すること。乗車中はプレイヤーのメッシュが非表示になり、`CameraPivotFollower` は Root 基準へ戻る。
5. 順序を疑う場合は `CinemachineCore.CameraUpdatedEvent` と `CameraController.LateUpdate` に `Time.frameCount` 付きログを入れ、同一フレームでリグ → Brain の順になっていることを見る。

## 7. 未検証事項

- コンパイル検査・実機確認は未実施 (spawn の cwd 制限で検査ホストが使えない)。
- `NetworkTRSP.InterpolationTarget` の API 名は Fusion 2 ドキュメントに基づく。Fusion 本体がリポジトリ外のためコンパイルで確認できていない。
