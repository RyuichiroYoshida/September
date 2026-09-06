# 4 人プレイ想定 パフォーマンス検証レポート

- 対象: September (Unity 6 / URP 17.0.4 / Photon Fusion 2 Host モード)
- 検証日: 2026-09-07
- 対象ブランチ: `fix/camera-timing-order` (618ccfdd 時点)
- 対象シーン: `Assets/Scenes/Maps/Museum/InGameMock.unity` + `Assets/Scenes/Maps/Museum/Field.unity` (加えて `Maps/Pirate/Pirate_Field.unity`)

---

## 0. 検証方法と限界

### やったこと
アセット (prefab / scene / ProjectSettings / URP アセット) の YAML を機械的に集計し、
併せてランタイムスクリプト 546 本を対象に「毎フレーム / 毎ティック実行される経路」を
呼び出しグラフ 2 段まで展開して重い API を洗い出した。数値はすべて実ファイルからの実測値。

### やっていないこと (と、その理由)
- **実機プロファイリング (Unity Profiler / RenderDoc) は未実施。** Session ポリシーにより
  明示指示のない起動・実行テストを行っていない。また当リポジトリのチェックアウトには
  Photon Fusion 本体 (`Assets/Photon`) が含まれておらず (外部アセット取得が別手順)、
  そのままではコンパイル・起動できない。
- したがって本レポートは **「4 人時にどこが効くか」の静的な特定と規模見積もり** であり、
  ミリ秒単位の実測値ではない。§8 に実測手順を用意したので、対処の前後で必ず突き合わせること。

### 4 人プレイでの前提
- `GameMode.Host` (`Assets/Scripts/Common/NetworkManager.cs:65`)。
  **ホストは 4 人分のシミュレーションを回しながら自分の画面も描画する。**
  つまりホスト機が常に最悪ケースであり、性能問題はまずホストで顕在化する。
- Area of Interest / Interest Management は未設定 (プロジェクト内に該当 API 呼び出しなし)。
  4 人・単一マップなら妥当だが、全員が全オブジェクトの状態を受け取る前提になる。

---

## 0-2. 本 PR に含まれる変更

検証レポート本体に加え、**第 1 波のうち Unity Editor 操作を伴わないもの**を実装した。

| ファイル | 変更 |
|---|---|
| `Assets/Settings/PC_RPAsset.asset` | 追加ライトシャドウアトラス 8192 → 4096 / ソフトシャドウ品質 High → Medium / シャドウカスケード 4 → 2 |
| `Assets/Settings/PC_Renderer.asset` | SSAO `Downsample: 0 → 1` |
| `Assets/Scripts/Common/Performance/FrameRateSettings.cs` (新規) | `Application.targetFrameRate = 60` |
| `Assets/Scripts/Common/Performance/LogStackTraceSettings.cs` (新規) | 製品ビルドのみ Log / Warning のスタックトレースを None に |

**入れなかったもの** (理由は各章):
- **Field.unity のライトベイク** — 最重要だが Unity Editor 操作が必要 (§2-1)
- **不透明テクスチャ OFF / デカール Feature 削除** — 実際に使用中だった。当初案の訂正 (§2-3)
- **物理レイヤ衝突マトリクスの整理** — 実害が無いことが判明。当初評価の下方修正 (§3-1)

いずれも実機未検証。マージ前に少なくとも博物館マップの見た目 (特に影) を目視確認すること。

---

## 1. 結論サマリ

影響度順。詳細は各章。

| # | 問題 | 主因 | 4 人時の効き方 | 影響 | 難度 |
|---|------|------|--------------|------|------|
| 1 | Field.unity にライトベイクデータが存在しない | ライト 126 個が実質リアルタイム扱い、うち 54 個がソフトシャドウ付きポイントライト | プレイヤー数と無関係に常時。全員に効く | **致命** | 低 |
| 2 | キャラクターのメッシュが分割されすぎ | 猿飛 = SkinnedMeshRenderer **113 個**、谷平 18、岡部 13 (ハルのみ 1) | ほぼ線形に ×4 | **致命** | 中 |
| 3 | 追加ライトシャドウアトラス 8192 + ソフトシャドウ最高品質 + カスケード 4 | `PC_RPAsset.asset` | 常時 | **大** | 低 — **本 PR で対応済み** |
| 4 | ~~物理レイヤ衝突マトリクスが全 ON~~ → **実害なしと判断 (下方修正)** | 動的ボディがプロジェクト全体で 14 個 (非キネマティック 9 個) しかなく、PhysX は静的同士の接触ペアを生成しない | ほぼ効かない | 小 | 対応不要 |
| 5 | 近接判定のレイヤマスクが `~0` (全レイヤ) | `MeleeHitboxExecutor.cs:23` | 攻撃中のプレイヤー数だけ加算 | **大** | 低 |
| 6 | エフェクト・ダメージテキストがプール無しで Instantiate/Destroy | `EffectSpawner.cs` / `DamageTextFactory.cs` | 戦闘中は交戦人数の二乗的に増える | **大** | 中 |
| 7 | ランキングをスコア変化のたびに全再計算 (例外を制御フローに使用) | `Ranking.cs:54-` ← `PlayerDatabase` の `OnChangedRender` | 被弾・与ダメのたび、全 4 クライアントで発火 | **大** | 低 |
| 8 | 接地判定を 1 ティックに 2 回、各 2 クエリ | `PlayerMovement.cs:154, 202` | 最大 4 クエリ×4 人×50Hz (+ 再シミュ) | 中 | 低 |
| 9 | タイマー UI を毎ティック `text` 書き換え | `InGameStatusView.cs:227, 244` | 常時 (TMP 再メッシュ 50 回/秒) | 中 | 低 |
| 10 | Debug.Log がスタックトレース付きで有効 (371 箇所) | `ProjectSettings.asset` `m_StackTraceTypes` | 常時 | 中 | 低 — **本 PR で対応済み** |
| 11 | LODGroup がプロジェクト全体でゼロ | prefab / scene ともに 0 件 | 遠景プレイヤー・遠景什器が常にフル解像度 | 中 | 中 |
| 12 | ダメージテキスト RPC が全員宛てブロードキャスト | `DamageTextFactory.cs:19` | 3/4 が捨てる無駄配信 | 小〜中 | 低 |
| 13 | vSync 無効かつ `targetFrameRate` 未設定 | QualitySettings / コード内に指定なし | 常時 (フレームペーシング不安定・発熱) | 小 | 低 — **本 PR で対応済み** |
| 14 | テクスチャ 250 枚中 246 枚が maxSize 2048 | VFX のグラデーション等も 2048 | VRAM / 帯域 | 小 | 低 |

---

## 2. 描画 (GPU) — 最大のボトルネック

### 2-1. 【致命】Field.unity にライトのベイク結果が無い

`Assets/Scenes/Maps/Museum/Field.unity` の `LightmapSettings`:

```
m_EnableBakedLightmaps: 1
m_MixedBakeMode: 2          # Shadowmask
m_LightingDataAsset: {fileID: 0}   # ← ベイク結果が無い
lightmap entries: 0
```

一方で `Assets/Scenes/Maps/Museum/InGameMock.unity` には `InGameMock/LightingData.asset` が存在する。
**ジオメトリを持つのは Field 側 (MeshRenderer 543 / GameObject 950) なのに、ベイクされているのは
ほぼ空の InGameMock 側**という状態で、マップ移設後にベイクし直されていないと見られる。

実体の `Assets/Prefabs/3DModel/Field (1).prefab` に含まれるライト 126 個の内訳:

| 種別 | Lightmapping | 影 | 個数 |
|------|-------------|----|------|
| Point | Mixed | Soft | **54** |
| Point | Baked | Soft | 39 |
| Point | Mixed | なし | **12** |
| Spot | Baked | なし | 14 |
| Spot | Baked | Soft | 6 |
| Directional | Baked | Soft | 1 |

- Mixed ライト **66 個** はベイクデータが無い以上、実行時は完全リアルタイム扱いになる。
- うち **54 個がシャドウキャスト付きポイントライト**。ポイントライトの影はキューブ 6 面なので
  最悪 **324 枚のシャドウマップ描画**が毎フレーム走る。
- Baked 指定の 60 個はベイクデータが無いためプレイヤービルドでは何も照らさない。
  つまり **見た目もベイク前提の想定と食い違っている**可能性が高い。

**対処**
1. Field.unity (もしくは Field+InGameMock をまとめた状態) でライトベイクを実行し、
   `LightingData.asset` を生成する。Field prefab 側は 950 GameObject 中 605 が Static 指定済みなので
   ベイク自体はすぐ通るはず。
2. ベイク後、Mixed のうち「動くものを照らす必要がないもの」は Baked に落とす。
   Mixed のまま残すのは主要ライト数個に限定する。
3. 屋内点光源のソフトシャドウは、ベイク後はほぼ不要になる。`Shadow Type: None` へ。

これだけで GPU 時間は大幅に落ちるはず。**最優先。**

### 2-2. 【致命】キャラクターの SkinnedMeshRenderer が分割されすぎている

| キャラ prefab | SkinnedMeshRenderer | 全て有効 | 全て影キャスト | 参照マテリアル種 | LODGroup |
|---|---|---|---|---|---|
| `Character Sarutobi.prefab` | **113** | ○ | ○ | **3 種のみ** | 0 |
| `Character Tanihira.prefab` | 18 | ○ | ○ | – | 0 |
| `Character Okabe.prefab` | 13 (各 83 ボーン) | ○ | ○ | – | 0 |
| `Character Haru.prefab` | **1** (106 ボーン) | ○ | ○ | – | 0 |
| `Templates/PlayerBase.prefab` | 13 (各 83 ボーン) | ○ | ○ | – | 0 |

猿飛の 113 個は `polySurface1`, `polySurface11`, … という名前で、DCC からの
エクスポート時にパーツごとに別スキンメッシュとして出ている状態。
**マテリアルは 3 種類しか使っていないので、3 個以下に統合できる。**

4 人時の概算 (影パス込みで約 2 倍):

- 全員猿飛: 113 × 4 = 452 renderer → 約 **900 draw call**
- 4 キャラ 1 人ずつ: 113+18+13+1 = 145 renderer → 約 **290 draw call**
- 目標 (全員 1〜3 メッシュに統合): 4〜12 renderer → 約 **24 draw call**

SRP Batcher は有効 (`m_UseSRPBatcher: 1`) なので 1 draw call あたりのセットアップは安いが、
**draw call 数そのものと、113 回のスキニングディスパッチは減らない。**
ハルだけが 1 メッシュで正しく作られているので、他 3 キャラを同じ作り方に揃えるのが正解。

**対処**
1. 猿飛・谷平・岡部の FBX をマテリアル単位でメッシュ統合し直す (DCC 側 or `Mesh.CombineMeshes` の
   エディタ拡張)。優先度は 猿飛 ≫ 谷平 > 岡部。
2. 全キャラに LODGroup を付ける。少なくとも「遠距離では影キャストを切る」だけでも効く。
3. `PlayerNameUI` に `_maxDistance = 30f` が宣言されているのに未使用 (`PlayerNameUI.cs:12`)。
   同じ距離しきい値でキャラ本体の影・IK も落とすと一貫する。

### 2-3. 【大】URP アセットの設定が過剰 — 本 PR で一部対応済み

`Assets/Settings/PC_RPAsset.asset`:

| 設定 | 変更前 | 変更後 | 所見 |
|---|---|---|---|
| `m_AdditionalLightsShadowmapResolution` | **8192** | **4096** | ポイントライト影用アトラス。8192×8192 は VRAM を大きく食い、クリアコストも高い |
| `m_SoftShadowQuality` | 3 (High) | **2 (Medium)** | PCF タップ数最大だった。Medium で見分けはつきにくい |
| `m_ShadowCascadeCount` | 4 (メイン 2048) | **2** | 屋内主体なので 2 で足りる |
| `m_RequireOpaqueTexture` | 1 | 1 (据え置き) | **使用中のため変更不可** (下記) |
| `m_RequireDepthTexture` | 1 | 1 (据え置き) | SSAO / アウトラインが使うので必要 |
| `m_ShadowDistance` | 50 | 50 (据え置き) | QualitySettings 側は 40。URP アセット側が優先されるため二重管理が紛らわしい。値の意図を確認したいので今回は触っていない |

`Assets/Settings/PC_Renderer.asset` の SSAO を `Downsample: 0 → 1` に変更 (実質半額)。
`BlurQuality: 0` (High) はビジュアル変化を抑えるため据え置き。

> **当初案からの訂正 (実装前調査で判明)**
> - **`m_RequireOpaqueTexture` は OFF にできない。** `_CameraOpaqueTexture` を
>   `Graphics/SeaData/ShaderGraph/WaterBaseGraph/StylizedWaterShader.shadergraph` (海面)、
>   `VFX/VFX/mate/FXM_distortion.shadergraph` (歪み)、
>   `VFX/VFX/mate/Opticalflage/FXM_OpricalCamouflage.shadergraph` (光学迷彩) ほか計 5 本が使用中。
>   切ると海面と歪み系エフェクトが壊れる。
> - **`DecalRendererFeature` も外せない。** `Prefabs/Kraken/KrakenAttackPrediction.prefab` と
>   `VFX/TakamuraRiho/pfb/Craken/VFX_Craken_Break.prefab` が DecalProjector を使用中
>   (クラーケンの攻撃予測表示)。

`Assets/Settings/Mobile_RPAsset.asset` は元から追加ライト影 OFF・カスケード 1・ソフトシャドウ OFF と
十分保守的なので変更していない。

なお Rendering Mode は Forward+ (`m_RenderingMode: 2`) なので、**ライトの「本数」自体は Forward+ が
うまく捌く。問題はあくまで「影を落とすライトの本数」**である点に注意 (→ §2-1)。

> **注意: シャドウアトラス縮小とライトベイクは対で行うこと。**
> 現状は §2-1 の通りシャドウキャストするポイントライトが 54 個 (= 最大 324 面) 残っているため、
> アトラスを 8192 → 4096 にすると 1 面あたりのタイル解像度が落ち、影のジャギが目立つ可能性がある。
> ベイクを実施して影キャストライトを数個まで減らせば問題にならない。
> **ベイク前に見た目を確認して耐えられないようなら、ベイク完了までは 8192 に戻してよい。**

### 2-4. その他

- **LODGroup がプロジェクト全体で 0 件** (prefab / scene 全走査)。博物館の 543 メッシュもキャラも
  常に最大解像度で描かれている。
- Field prefab の MeshRenderer 543 個中 **522 個が影キャスト ON**。小物・壁の裏など、
  影を落とす必要のないものを落とすだけで shadow pass のドローが減る。
- テクスチャ 250 枚中 **246 枚が maxTextureSize 2048**。圧縮は効いているが、
  `Graphics/VFX/Shirai/Tex/simple_gradient.png` (元 16.7MB) のようなグラデーションが 2048 なのは無駄。
  VFX テクスチャを 256〜512 に落とすだけで VRAM と帯域が空く。
- `Assets/Prefabs/Kraken/BgMD_Ship_Army_Test_01.prefab` は **MeshCollider 214 個**。
  海賊マップで使うならコライダーを簡易形状に置き換えるべき (ロード時の PhysX ベイクとクエリコストの両方に効く)。

---

## 3. 物理

### 3-1. レイヤ衝突マトリクスが全 ON — ただし**実害は小さい (当初評価を下方修正)**

`ProjectSettings/DynamicsManager.asset` の `m_LayerCollisionMatrix` を展開したところ、
**使用中の 15 レイヤすべてが 32 レイヤすべてと衝突する設定**になっていた。

```
layer  0 Default            collides with 32 layers
layer  5 UI                 collides with 32 layers
layer  6 Player             collides with 32 layers
layer 11 OutLine            collides with 32 layers
layer 14 ScanTarget         collides with 32 layers
...
```

当初これを「大」と評価したが、実装前に動的ボディ数を数えたところ **評価が過大だった**。

コライダーとリジッドボディの実測分布 (prefab / scene 全走査):

| レイヤ | コライダー数 | Rigidbody 数 (うちキネマティック) |
|---|---|---|
| 0 Default | 259 | 4 (1) |
| 6 Player | 6 | 3 (0) |
| 7 Ground | 46 | 0 |
| 11 OutLine | 23 | 5 (4) |
| 12 JewelryBlocker | 50 | 0 |
| 10 Jewelry | 4 | 2 (0) |
| その他 (TransparentFX/Ignore Raycast/AirPlane/KrakenHit/ScanTarget) | 各 1〜9 | 0 |
| 4 Water / 5 UI / 8 TogeToge / 9 UI_NoPostProcess | **0** | 0 |

**Rigidbody はプロジェクト全体で 14 個 (非キネマティック 9 個) しかない。**
PhysX のブロードフェーズは「少なくとも片方が非静的アクタ」のペアしか生成しないため、
静的コライダー同士 (什器 × 壁 × JewelryBlocker など) は行列の設定に関わらずコストゼロ。
実際に生成されるペアは 4 人のプレイヤーと数個の宝石・砲弾まわりに限られる。

**したがって本 PR ではマトリクスを変更していない。**
投機的に組み合わせを切ると、意図を確認できないままゲームプレイを壊すリスクの方が大きい。

**将来やるなら** (ゲームプレイ担当の確認が要る、性能ではなく設計衛生の観点):
`Water` / `UI` / `TogeToge` / `UI_NoPostProcess` は現状コライダーが 1 つも無いので、
将来うっかり物理に参加しないよう OFF にしておく価値はある。
`OutLine` / `ScanTarget` / `JewelryBlocker` の組み合わせは意図の確認が必要。

> 物理クエリ (`Physics.Overlap*` / `Raycast`) はこのマトリクスではなく
> **呼び出し側の layerMask** に従う。実際に効くのは次の §3-2 の方。

### 3-2. 【大】近接判定が全レイヤを対象にしている

`Assets/Scripts/InGame/Common/Hitbox/MeleeHitboxExecutor.cs:23`

```csharp
_hitMask = hitMask == 0 ? ~0 : hitMask;
```

呼び出し側の `HitChecker.cs:21` は `hitMask` を渡していないため、**常に `~0` (全レイヤ)** になる。
さらに `QueryTriggerInteraction.Collide` 指定でトリガーも拾う。
`Physics.OverlapCapsuleNonAlloc` が博物館の壁・床・什器・UI コライダーまで走査している。

加えて:
- `HitChecker.StartHitCheck()` が攻撃のたびに `new MeleeHitboxExecutor(...)` を確保 (`HitChecker.cs:21`)。
- `HitChecker` 側は `List<Collider>.Contains` で重複除外 (O(n))、`MeleeHitboxExecutor` 側は
  `HashSet` で同じことをしており二重管理。
- クラーケンは HitChecker を 6 個持つ (`Kraken.prefab`)。

**対処**: `Player` / `KrakenHit` 等に絞った LayerMask を `HitChecker` に SerializeField で持たせて渡す。
Executor は使い捨てにせず再利用する。

### 3-3. 接地判定が 1 ティック 2 回

`PlayerManager.cs:153` が `UpdateMovement()`、`:157` が `MoveTick()` を同一ティックで呼び、
その両方が `CheckGroundManual()` を呼ぶ (`PlayerMovement.cs:154`, `:202`)。
`CheckGroundManual` → `TryProbeGround` は `Physics.Raycast` を撃ち、外れたら `Physics.SphereCast` で補完
(`PlayerMovement.cs:686`, `:706`)。

→ **1 ティックあたり最大 4 クエリ / プレイヤー**。4 人 × 50Hz で 800 クエリ/秒、
さらに Fusion のクライアント側再シミュレーションで自機分は数倍される。

**対処**: 1 ティック内で結果をキャッシュし、`Runner.Tick` が変わるまで再計測しない。

### 3-4. 村正の攻撃判定

`Assets/Scripts/InGame/Exhibit/MuramasaInteractInvoker.cs:31`

```csharp
var hits = Physics.OverlapSphere(...);           // ← アロケートする版
PlayerDatabase.Instance.PlayerObjectDic.TryGet(_currentOwner, out var player);
FormationManager formationManager = player.GetComponent<FormationManager>();   // 毎回 GetComponent
foreach (var hit in hits) { var friendBase = hit.GetComponent<FriendBase>(); ... }  // ヒット毎に GetComponent
```

0.2 秒間隔なので頻度自体は高くないが、`OverlapSphere` は毎回 `Collider[]` を確保する。
`OverlapSphereNonAlloc` + `FormationManager` のキャッシュ + `TryGetComponent` に置き換える。

---

## 4. GC / メモリ

### 4-1. 【大】エフェクトにオブジェクトプールが無い

`Assets/Scripts/InGame/Effect/EffectSpawner.cs`

```
192, 197, 263, 267:  Instantiate(effectData.Prefab, ...)
216, 285:            main.stopAction = ParticleSystemStopAction.Destroy;
306, 325:            Destroy(effect);
```

すべてのヒットエフェクト・ループエフェクトが都度 Instantiate → Destroy。
CLAUDE.md の「オブジェクトプールでガベージコレクションを最小化」という方針と矛盾している。
4 人が交戦している間、ヒット数に比例して ParticleSystem の生成・破棄が走り、
`gcIncremental: 1` でも回収しきれない波が出る。

### 4-2. 【大】ダメージテキストも都度生成 + 全員宛て RPC

`Assets/Scripts/InGame/DamageTextUI/DamageTextFactory.cs`

```csharp
[Rpc(RpcSources.All, RpcTargets.All)]                       // :19  全員に配信
private void RPC_ConfigureDamageText(int damage, PlayerRef playerRef)
{
    if (Runner.LocalPlayer.PlayerId == playerRef.PlayerId)  // :22  3/4 のクライアントは捨てる
        CreateDamageText(damage);
}
...
DamageTextBox parentObject = Instantiate(_damageTextAnim, _canvas.transform);   // :54
parentObject.GetComponentInChildren<TextMeshProUGUI>().text = damage.ToString(); // :55
Destroy(parentObject, _textDestroyTime);                                        // :62
```

- 1 ヒットごとに 4 クライアント全部へ RPC が飛び、3 つは捨てられる。
  `[RpcTarget]` で対象プレイヤーだけに送るべき。
- 表示側は Instantiate + `GetComponentInChildren` + `Destroy`。加えて
  親 Canvas に子が増減するので **ヒットのたびに Canvas 全体がリビルド**される。
- `Awake` で `GameObject.Find("DamageTextCanvas")` (`:35`)。スポーン時のみだが避けたい。

**対処**: プール化 + TMP の参照を prefab 側で直接持つ + ダメージ数字専用 Canvas を分離。

### 4-3. 【大】ランキングをスコア変化のたびに全再計算

`PlayerDatabase.PlayerDataDic` は `[Networked, OnChangedRender(nameof(OnChangedPlayerData))]`
(`PlayerDatabase.cs:25`)。値が変わるたび全クライアントで `ChangedDataAction` が発火し、
購読者の `Ranking.CalculateRanking` (`Ranking.cs:54`) が走る。

問題は `SessionPlayerData` に **戦闘中に毎回変わるフィールドが入っている**こと:

```csharp
public int Score;
public int DamageReceived;      // PlayerHealth.cs:62  被弾のたび
public int DamageDealt;         // PlayerHealth.cs:61  与ダメのたび
public int TotalInteractCount;
```

つまり**ヒット 1 回につき 2 回 dictionary が書き換わり、4 クライアント全部でランキングが再計算**される。
その中身が:

```csharp
var playerResults = new List<KeyValuePair<PlayerRef, PlayerResultEntry>>();  // :63  毎回 List 確保
foreach (...) {
    try { score = scorePolicy.GetScore(player); }
    catch (Exception) { score = sessionPlayerData.Score; }                   // :73  例外を制御フローに使用
    var entry = new PlayerResultEntry(sessionPlayerData.DisplayNickName, ...);// DisplayNickName は $"{}_{}"  で毎回文字列生成
}
```

- `List` と 4 個の `PlayerResultEntry`、および `DisplayNickName` の文字列補間が毎回発生。
- **`try`/`catch` を制御フローに使っている**。`GetScore` が日常的に投げる状況
  (オブジェクト未生成など、コメントにそう書いてある) だと、例外 1 回あたり数マイクロ秒〜のコストが
  ヒットのたび × プレイヤー数 × クライアント数で乗る。**スパイクの原因になりうる。**

**対処**
1. 表示用ランキングを「変化のたび」ではなく「一定間隔 (0.25〜0.5 秒) またはランク順位が変わったとき」だけ再計算する。
2. `GetScore` を `TryGetScore` に変えて例外を使わない。
3. `DisplayNickName` はキャッシュする (ニックネームは試合中不変)。
4. `List` を使い回す。

### 4-4. Debug.Log がスタックトレース付きで有効

- 非 Editor コードに `Debug.Log*` が **371 箇所**。
- `ProjectSettings/ProjectSettings.asset` の `m_StackTraceTypes: 010000000100...` は
  全ログ種別が **ScriptOnly** (= スタックトレース取得あり)。ログ 1 行ごとにマネージドスタックの
  文字列化が走る。

特に毎フレーム / 高頻度経路にあるもの:

| 場所 | 内容 |
|---|---|
| `PlayerMovement.cs:529` | `Debug.Log(reason)` — ジャンプ入力のたび (乗り越え判定の失敗理由) |
| `PlayerInteractionController.cs:230` | `Debug.Log("Nullにする")` — フォーカス外れのたび |
| `AnimationClipPlayer.cs` | 27 箇所 (再生・初期化のたび) |
| `PhotonSpawner.cs` | 11 箇所 |
| `EffectSpawner.cs` | 9 箇所 |

**対処 (本 PR で実施)**: `Assets/Scripts/Common/Performance/LogStackTraceSettings.cs` を追加し、
`!UNITY_EDITOR && !DEVELOPMENT_BUILD` のときだけ Log / Warning のスタックトレースを None にする。
Error / Assert / Exception は原因追跡に必要なので変更しない。

**残作業**: 恒常的に出るログ (上表の 5 箇所) は `[Conditional("UNITY_EDITOR")]` を付けたラッパへ寄せる。
スタックトレースを切っても文字列生成と IO のコスト自体は残るため。

---

## 5. スクリプト — 毎フレーム / 毎ティックの経路

呼び出しグラフを 2 段展開して重い API を検出した結果 (Editor 配下を除く)。

### 5-1. `InGameStatusView` タイマー UI が毎ティック TMP を書き換える

`Assets/Scripts/InGame/UI/AlphaUI/InGameStatusView.cs:227, 244`

```csharp
while (runner.Tick < gameEndTick)
{
    if (runner.Tick == lastTick) { await UniTask.Yield(...); continue; }
    int seconds = Mathf.CeilToInt(remaining / (float)tickRate);
    timer.text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");   // 毎ティック = 約 50 回/秒
    await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
}
```

**表示が変わるのは 1 秒に 1 回なのに、毎ティック `TimeSpan.ToString()` (文字列確保) と
TMP の再メッシュ + Canvas リビルドが走っている。**
`lastTick` を更新していないので毎ティック通る点にも注意 (`lastTick` は初期化後に代入されていない)。

**対処**: `seconds` が前回と変わったときだけ代入する。1 行で 50 分の 1 になる。

### 5-2. `PlayerInteractionController.Update` — ローカルプレイヤーとボット全員分

`Assets/Scripts/InGame/Interact/PlayerInteractionController.cs:222-266`

```csharp
int count = Physics.OverlapSphereNonAlloc(...);                    // :238  ← NonAlloc なのは良い
for (int i = 0; i < count; i++) {
    var interactable = go.GetComponentInParent<InteractableBase>()  // :246
                    ?? go.GetComponent<InteractableBase>()          // :247
                    ?? go.GetComponentInChildren<InteractableBase>();// :248  ← 再帰探索。外れヒットごとに走る
}
```

毎フレーム、ヒットしたコライダーの数だけ最大 3 回の `GetComponent*` (うち 1 つは子階層再帰)。
`~0` ではないもののマスクは広く、什器の多い博物館では外れヒットが多い。
また `UpdateInteractUI()` から `UIController.I.ShowInteractUI(...)` が**毎フレーム**呼ばれる。

**対処**: `InteractableBase` 側に `Collider → InteractableBase` の逆引き辞書を持たせるか、
コライダーに `InteractableRef` を 1 つ付けて `TryGetComponent` 一発にする。
UI 更新は状態が変化したときだけに絞る。

### 5-3. `InteractUi` が毎フレーム `Camera.main` + Canvas を動かす

`Assets/Scripts/InGame/UI/AlphaUI/InteractUi.cs:37-95`

`LastPostLateUpdate` で毎フレーム `Camera.main` (`:74`) を引き、
`_root.gameObject.SetActive()` と `_root.anchoredPosition` を書き換える (= Canvas リビルド)。
カメラ参照はキャッシュできる (プロジェクトには既に
`Assets/Scripts/Common/CameraView/CameraViewResolver.cs` という解決役がいる)。

### 5-4. `PlayerNameUI` / `CanvasRotator` — 距離カリング無し

- `PlayerNameUI.cs:44` LateUpdate で毎フレーム World Space Canvas を回転 (= Canvas dirty)。
  他プレイヤー 3 人分。`_maxDistance = 30f` (`:12`) が**宣言だけされていて使われていない**。
- `CanvasRotator.cs` も同様に無条件 LateUpdate。`_camera` の null チェックが無いのでカメラ差し替え時に例外の恐れ。

### 5-5. `FriendPlayerDetector` (谷平) — 再索敵時に LINQ チェーン

`Assets/Scripts/InGame/Player/Tanihira/FriendPlayerDetector.cs:80-115`

`OverlapSphereNonAlloc` は良いが、その後 `Take / Select / Where / Distinct / Concat / OrderBy` を
連結しており、**再索敵のたびに 6〜10 個のイテレータオブジェクトを確保**する。
ターゲットが無効な間は毎ティック走る。谷平が 4 人いれば 4 倍。

### 5-6. `StatusEffectManager.Update` — 全プレイヤー分

`Assets/Scripts/InGame/Common/StatusEffect/Common/StatusEffectManager.cs:17`
毎フレーム `UpdateEffects` → 内部で LINQ。プレイヤー 1 人につき 1 インスタンス。
`Profiler.BeginSample` が本番コードに残っている点も要確認 (Development ビルド以外では実質無害)。

### 5-7. `PlayerAudioController.Update` — デバッグ専用コードが本番に残存

`Assets/Scripts/InGame/Player/PlayerAudioController.cs:233-285`
`// デバッグ用 音実装終わるまで残す` のコメント付きで、レガシー `Input.GetKey` による
音量デバッグが全プレイヤー prefab 上で毎フレーム動いている。
`activeInputHandler: 2` (Both) なので例外にはならないが、
**新旧両方の入力バックエンドが常時動いている**状態でもある。`#if UNITY_EDITOR` で囲むべき。

### 5-8. クラーケン (出現時のみだが重い)

`Assets/Prefabs/Kraken/Kraken.prefab`: MonoBehaviour 111 個 / Animator 7 個 /
`TentacleConstraintSolver` **8 個** / `IKFollower` 6 / `ChainAimConstraint` 6 / `HitChecker` 6。
`TentacleConstraintSolver.cs:23` は `_pbdIterations = 5` × サブステップ × セグメント数のループを
8 本分回す。出現中のフレームコストは相当大きいので、**クラーケン出現時のフレームレートは
別途単独で計測する**こと。

なお `KrakenEvent.cs:48, 50` の LINQ (`// 非効率` とコメント済み) は出現判定が成立した
1 ティックだけなので実害は無い。

---

## 6. ネットワーク (Fusion)

- **Host モード**: ホストは 4 人分の `FixedUpdateNetwork` を回しつつ描画も行う。
  上記 §3-3 (接地判定 4 クエリ/tick) や §5 のティック処理はすべてホストで 4 倍になる。
  **性能検証は必ずホスト機で行うこと。**
- **`NetworkMecanimAnimator`** がプレイヤー prefab に付いている (`TotalWords: 2`, `SyncSettings: 79`)。
  Fusion の Mecanim 同期は帯域・CPU ともに高い部類。`AnimationClipPlayer` (PlayableGraph) と
  併存しているので、**両方が本当に必要かを確認**したい。片方に寄せられるなら 4 人分の同期が丸ごと消える。
- **`SessionPlayerData` に高頻度更新フィールドが同居** (§4-3)。
  `Score` / `DamageDealt` / `DamageReceived` / `TotalInteractCount` を
  リザルト集計用の低頻度データと分離すれば、`OnChangedRender` の発火頻度が桁で下がる。
- `SessionPlayerData` は `NetworkDictionary<PlayerRef, int> StunData` (Capacity 7) を**入れ子で**持ち、
  それ自体が `NetworkDictionary<PlayerRef, SessionPlayerData>` (Capacity 8) の値になっている。
  1 プレイヤー分の state が大きく、どこか 1 フィールドの変更でも周辺ワードが dirty になりやすい。
- `[Rpc(RpcSources.All, RpcTargets.All)]` の全体ブロードキャストが散見される
  (代表: `DamageTextFactory.cs:19`)。受け手側で `if (LocalPlayer == target)` して捨てているものは
  `[RpcTarget]` 指定に変える。
- Interest Management 未使用。4 人なら許容だが、宝石・什器・ペンギン隊列を含めた
  NetworkObject 総数が増えるなら再検討の余地あり。

---

## 7. ビルド / プロジェクト設定

| 項目 | 現在値 | 所見 |
|---|---|---|
| `Application.targetFrameRate` | 未設定 → **60 に設定** (`FrameRateSettings.cs`) | 値は暫定。120/144 にしたい場合は定数 1 箇所を変えるだけ |
| `vSyncCount` (Mobile / PC) | 0 / 0 (据え置き) | vSync を有効にした環境では targetFrameRate 側が無視されるだけなので競合しない |
| `m_StackTraceTypes` | 全て ScriptOnly (据え置き) | 製品ビルドのみ実行時に Log/Warning を None へ (`LogStackTraceSettings.cs`)。ProjectSettings を直接変えると Editor でもログからソース行に飛べなくなるため、実行時適用にした |
| `gcIncremental` | 1 | 良い |
| `activeInputHandler` | 2 (Both) | 新入力システムに一本化できるならした方が良い |
| `m_AutoSyncTransforms` | 0 | 良い |
| `m_ReuseCollisionCallbacks` | 1 | 良い |
| Fixed Timestep | 0.02 (50Hz) | 妥当 |
| QualitySettings | Mobile / PC の 2 段。影距離 40、カスケード 2 | URP アセット側 (50 / 4) と食い違っている。URP アセットが優先されるので QualitySettings 側の値は誤解のもと |

---

## 8. 実測手順 (このレポートの検証と、対処後の確認)

静的解析だけでは「どれが何 ms か」は決まらない。以下を実施して数値を取ること。

### 8-1. 計測構成
1. ホスト 1 + クライアント 3 (実機 4 台が理想。難しければ ParrelSync で同一機に 4 プロセス。
   ただし同一機だと GPU を取り合うので **描画コストの判断はホスト実機単独で**行う)。
2. 計測は Development Build + Autoconnect Profiler。Editor 計測は Editor オーバーヘッドが乗るため参考値。
3. シーンは `Maps/Museum/InGameMock` + `Field`。海賊マップは別途。

### 8-2. 取るべき数値
| 指標 | 見る場所 | 判断基準 |
|---|---|---|
| CPU Main / Render / GPU 時間 | Profiler タイムライン | どちらが律速かをまず切り分ける |
| SetPass Calls / Draw Calls / Batches | Rendering モジュール | §2-2 の見積もり (キャラだけで 290〜900) と突き合わせ |
| Shadow Casters / シャドウパス時間 | Frame Debugger | §2-1 の 54 ライト×6 面が出ているか確認 |
| GC Alloc / frame | Memory モジュール | 戦闘中に跳ねるなら §4 |
| `Physics.Processing` / `Physics.Queries` | CPU モジュール | §3 |
| `Fusion` の Simulation / 帯域 | `FusionStats` (プロジェクトに `PhotonTrafficLogger` あり、シーン未配置) | §6 |

### 8-3. 計測シナリオ
- (A) 4 人が離れて待機 — ベースライン
- (B) 4 人が同一部屋で乱戦 (エフェクト・ダメージテキストが飛び交う状態) — §4 の検証
- (C) 全員が猿飛 — §2-2 の検証
- (D) クラーケン出現中 — §5-8 の検証
- (B) と (A) の差が GC / ネットワーク由来、(C) と (A) の差が描画由来として切り分けられる。

### 8-4. 既存の計測資産
`Assets/Scripts/Common/PhotonTrafficLogger/` に通信量ロガーが実装済みだが、
**どのシーン・prefab にも配置されていない** (GUID 全走査で参照 0)。
帯域の実測にはこれをデバッグ用 prefab へ載せると良い。
ただし `PhotonTrafficLogger.Update` は `FindObjectsByType` と大量の `Debug.Log` を含むので、
**計測時のみ有効化**し、本番ビルドには入れないこと。

---

## 9. 推奨対応順

「効果 ÷ 手間」の順。

### 第 1 波 (設定変更中心。半日以内)

| # | 内容 | 状態 |
|---|---|---|
| 1 | **Field.unity をライトベイクする** (§2-1) — 最大の効果 | ⬜ **未実施** (Unity Editor 操作が必要。次にやるべきこと) |
| 2 | シャドウアトラス 8192 → 4096、ソフトシャドウ High → Medium、カスケード 4 → 2 (§2-3) | ✅ 本 PR |
| 3 | SSAO を `Downsample: 1` に (§2-3) | ✅ 本 PR |
| 3' | 不透明テクスチャ OFF / デカール Feature 削除 | ❌ **取り下げ** — 両方とも使用中 (§2-3 の訂正欄) |
| 4 | 物理レイヤ衝突マトリクスを整理 (§3-1) | ❌ **見送り** — 実害が無いことが判明。詳細は §3-1 |
| 5 | 製品ビルドの Debug.Log スタックトレースを None に (§4-4) | ✅ 本 PR (`LogStackTraceSettings.cs`) |
| 6 | `Application.targetFrameRate` を設定 (§7) | ✅ 本 PR (`FrameRateSettings.cs`) |

#5 / #6 は ProjectSettings だけでは表現できない (targetFrameRate に該当する project setting が無く、
`m_StackTraceTypes` は Editor にも一律に効いてしまいログからソース行へ飛べなくなる) ため、
`Assets/Scripts/Common/Performance/` にランタイム適用の小さなクラスを 2 つ追加している。

**#1 のライトベイクが第 1 波の本体である。** 本 PR の #2/#3 は補助にすぎないので、
ベイクを実施するまで大きな改善は見込めない (§2-3 の注意書きも参照)。

### 第 2 波 (小さいコード修正、各 1〜数時間)
7. `MeleeHitboxExecutor` のレイヤマスクを明示 (§3-2)
8. タイマー UI を秒が変わったときだけ更新 (§5-1)
9. 接地判定をティック内キャッシュ (§3-3)
10. ランキング再計算の間引き + 例外を制御フローから排除 (§4-3)
11. ダメージテキスト RPC を対象プレイヤー限定に (§4-2)
12. `PlayerAudioController` のデバッグ Update を `#if UNITY_EDITOR` で囲む (§5-7)
13. `PlayerNameUI` の `_maxDistance` を実際に使って距離カリング (§5-4)

### 第 3 波 (アセット・設計変更)
14. **猿飛 (113) / 谷平 (18) / 岡部 (13) のメッシュ統合** (§2-2) — 第 1 波の次に効果が大きい
15. エフェクト・ダメージテキストのプール化 (§4-1, §4-2)
16. キャラと什器への LODGroup 追加 (§2-4)
17. `SessionPlayerData` の高頻度フィールド分離 (§6)
18. `NetworkMecanimAnimator` と `AnimationClipPlayer` の重複解消可否を検討 (§6)
19. VFX テクスチャの maxSize 見直し (§2-4)

---

## 10. 未確認事項

- **実測値なし。** §8 の手順で必ず裏を取ること。特に「どれが律速か (CPU / GPU)」は
  静的解析では確定できない。
- Photon Fusion 本体が当チェックアウトに無いため、`NetworkProjectConfig` の
  **Tick Rate / Simulation 設定 / Replication 設定を確認できていない**。
  Fusion 導入済みの環境で確認が必要。
- `Assets/Plugins` / `Assets/Demo` / 一部 3D モデルは `.gitignore` 対象のため未調査。
  海賊マップの船体・海面 (`PB_Plane` + 海シェーダ) は外部アセット側にあり、
  シェーダコストを確認できていない。
- 使用中のキャラクターごとの実際の使用率 (全員猿飛になる頻度) が不明。
  §2-2 の見積もりは最悪ケースと平均ケースの両方を併記している。
