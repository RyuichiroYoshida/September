namespace InGame.Player
{
    /// <summary>
    /// プレイヤーPrefabを擬態によって破棄する直前に、Prefab外へ生成した物を片付けるためのフック。
    /// </summary>
    public interface IMimicCleanup
    {
        void CleanupBeforeMimicDespawn();
    }

    /// <summary>
    /// 試合途中に擬態先Prefabとして生成された後、参照交換完了時に追加初期化するためのフック。
    /// </summary>
    public interface IMimicInitialize
    {
        void InitializeAfterMimicSpawn();
    }
}
