using Fusion;
namespace InGame.Exhibit.HazardTrail
{
    /// <summary>
    /// <see cref="GroundHazard"/> に付与される個別の効果（ダメージ、デバフ、視覚演出など）を定義するインターフェース。
    /// </summary>
    public interface IHazardEffect
    {
        /// <summary>
        /// ハザード生成時の初期化
        /// </summary>
        void OnHazardSpawn(NetworkRunner runner, PlayerRef owner) { }
        /// <summary>
        /// FixedUpdateNetwork ごとに呼ばれる効果の更新処理
        /// </summary>
        void OnHazardTick(NetworkRunner runner, PlayerRef owner);
        /// <summary>
        /// ハザード破棄・終了時のクリーンアップ処理
        /// </summary>
        void OnHazardDespawn(NetworkRunner runner) { }
    }
}
