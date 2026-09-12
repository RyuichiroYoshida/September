# エイム入力・展示物搭乗中のロックオン禁止

- Date: 2026-09-12
- Status: fixed in working tree
- Area: PlayerManager / InputProvider

## Summary

エイム入力中および乗り物展示物への搭乗中にロックオンを禁止し、開始済みのロックオンも解除する。キャラクター追加時に個別対応を必要としない共通処理とする。

## Evidence

- `PlayerManager.UpdateLockOn` に Aim 入力の判定がなかった。
- 搭乗時は `ForcedControl` により新規開始とカメラ追従を抑止していたが、ロックオン状態と対象 ID が残っていた。
- `MountableExhibitBase`、`PropAirplane`、ジップラインは `SetControlState` を使用し、クラーケンも `RPC_SetInvisible` 経由で同じ状態遷移を使用する。
- `InputProvider.OnInput` は Move 無効時に Aim 入力も捨てていた。

## Regression Context

既存実装の制約不足。過去の正常動作からの回帰かどうかは未確認。

## Cause

ロックオンの保持・解除条件と、エイム入力・搭乗状態の切替が連携していなかった。

## Fix Requirements

- 共通 Aim 入力でロックオン開始を禁止し、既存状態と対象 ID を解除する。
- Aim を Move の有効状態と独立して収集する。
- 通常操作以外への遷移時に解除し、搭乗中は入力の有無にかかわらず保持しない。
- ローカルカメラも Aim 押下中は追従させない。
- エイム終了・降車だけでは再ロックオンしない。

## Verification

ソースと差分を静的に確認。session policy によりテスト・Unity 起動・コンパイルは実行していない。
実機確認項目: 全キャラの Aim 保持、Aim と LockOn 同時押し、ロックオン中の Aim 開始、Move 無効時の Aim、各展示物への搭乗と降車、搭乗直後の入力欠落、ホストとクライアントでの解除・再入力による再ロックオン。

## Follow-up

上記の実機動作は未確認。
