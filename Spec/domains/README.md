# ドメイン定義 (Anatomia ontology)

このディレクトリは Anatomia のドメイン定義 (DomainDef) を置く場所です。
Revisor のレビューでは、変更されたコードがいずれかのドメインに属していることが
必須条件になっており、定義が無いと `target domain is still missing` で
レビュー自体が開始されません。

## 各ファイルの意味

1 ファイル = 1 ドメインです。`membership.pathPattern` (正規表現、パス区切りは `/`)
に一致するファイルのコードが、そのドメインの実装として扱われます。

| ドメイン | 範囲 |
|---|---|
| `networking` | Photon Fusion の接続・セッション・同期基盤 (`Common/`, `NetworkTest/`) |
| `player` | プレイヤーの移動・状態・体力・アビリティ (`InGame/Player/`, `InGame/Health/`) |
| `kraken` | クラーケンの挙動・アニメーション・攻撃 (`InGame/Kraken/`) |
| `bot` | Bot の思考と行動制御 (`InGame/Bot/`) |
| `world-interaction` | インタラクト対象 (展示物・宝飾・海図・騎乗・建築) |
| `game-flow` | ゲーム進行・ルール・終了処理・シーン遷移 (タイトル/ロビー/リザルト) |
| `presentation` | UI・エフェクト・デカール・演出 |
| `rendering` | URP のレンダーフィーチャと VFX (`Assets/Graphics/`) |
| `input` | Input System のアクション定義 (`Assets/InputActions/`) |
| `editor-tooling` | Unity Editor 拡張 (`Assets/Scripts/Editor/`) |
| `debug-tooling` | 実行時デバッグ支援 (`Assets/Scripts/Debug/`) |

`Assets/Plugins/` 配下の外部ライブラリ (DOTween 等) は、このプロジェクトの
ドメインではないため意図的にどこにも割り当てていません。

## 新しいディレクトリを追加したとき

既存ドメインに属するなら該当ファイルの `membership` にパスを足してください。
どのドメインにも当てはまらない新しい関心事であれば、既存ファイルを複製して
`name` / `description` / `membership` / `rationale` を書き換えます。
`rationale` は「なぜこの範囲が 1 つのドメインなのか」を書く欄で、
レビュー時の判断材料になります。

## 確認方法

```
anatomia domains list        # 定義が読み込めているか
anatomia domain-review       # 各ドメインの実装数と未割り当て関数
```
