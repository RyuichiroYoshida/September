# コンパイル検査 (CLI)

PR を出す前に、Unity Editor を開かずに C# のコンパイルエラーが無いことを確認するための CLI。
`Tools/compile-check.ps1` が Unity を batchmode で起動してスクリプトをコンパイルさせ、ログを判定する。

## 使い方

```powershell
# リポジトリのルートで
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/compile-check.ps1
```

```bat
REM cmd から呼ぶ場合
Tools\compile-check.cmd
```

`Assets/` を含むプロジェクトルートが既定の検査対象になる。worktree など別の場所を見るときは `-ProjectPath` を渡す。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -ProjectPath E:\path\to\worktree
```

## 終了コード

| コード | 意味 |
| --- | --- |
| 0 | コンパイルエラーなし |
| 1 | コンパイルエラーあり (該当行を標準出力に表示) |
| 2 | 検査を実行できなかった (Unity 未検出・タイムアウトなど) |

CI から使う場合はこの終了コードで判定する。1 と 2 を区別しているのは、「コードが壊れている」のと「検査環境が整っていない」のを取り違えないため。

## 前提

- **Unity 6000.0.41f1** (`ProjectSettings/ProjectVersion.txt` の値) がインストールされていること。
  `C:\Program Files\Unity\Hub\Editor` と Unity Hub の「別の場所にインストール」設定から自動で探す。
  見つからない場合は `-UnityPath` で `Unity.exe` を直接指定するか、`UNITY_EDITOR_PATH` を設定する。
- **初回はアセットのインポートが走るため時間がかかる** (数十分規模)。`Library/` が作られた後は短くなるので、
  検査用のチェックアウトは使い捨てにせず残しておくとよい。既定のタイムアウトは 60 分で、`-TimeoutMinutes` で変更できる。

## リポジトリを汚さない

Unity はプロジェクトを開くと `ProjectSettings/ProjectVersion.txt` を自分のバージョンで上書きし、
バージョンが違えばパッケージも解決し直して `Packages/manifest.json` / `Packages/packages-lock.json` を書き換える。
検査の副作用でリポジトリが汚れないよう、スクリプトはこれらの内容を実行前に控えて実行後に復元する。
復元したときは警告を出す。

`Logs/compile-check.log` に Unity のログが残る (`Logs/` は `.gitignore` 済み)。

## 外部アセットが必要

Photon Fusion / CRI ADX / NaughtyAttributes / Final IK などの外部アセットは `.gitignore` 対象
(`/[Aa]ssets/Plugins/`、`Assets/CRIMW/`) でリポジトリに含まれず、Unity メニューの
**`September/Import` (アセットインポートツール)** から取得します。

そのため**クローンしただけの作業ツリーはコンパイルできません**。検査は、アセットをインポート済みの
チェックアウトに対して実行してください (`.github/workflows/build.cmd` が固定パスを `git pull` して
使い回しているのと同じ理由です)。

外部アセット由来の `CS0246` を検出した場合、スクリプトはコンパイルエラー (終了コード 1) ではなく
**検査不能 (終了コード 2)** として報告します。「コードが壊れている」と誤読しないためです。

## バージョンが違う Unity で動かす場合

要求バージョンが入っていないとき、既定では**実行せずに終了コード 2 で失敗する**。
同系列 (同じメジャー.マイナー) の別バージョンで代用したい場合だけ `-AllowVersionMismatch` を付ける。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -AllowVersionMismatch
```

ただしこの場合、Unity がパッケージを自分の対応バージョンへ解決し直すため、
**本来の依存関係とは違う組み合わせでのコンパイル結果**になる。参考値として扱い、
最終確認は要求バージョンで行うこと。

## CI への組み込み

既存の `.github/workflows/WinBuild.yml` は self-hosted Windows ランナー上で
`build.cmd` (`-executeMethod BuildCommand.Build`) を実行してビルドしている。
同じランナーで `Tools/compile-check.cmd` を PR トリガーで走らせれば、PR 時点のコンパイル可否を自動判定できる。
ランナーが 1 台のため、ビルドと同時に走ると詰まる点に注意。
