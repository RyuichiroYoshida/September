namespace InGame.Player
{
    /// <summary>
    /// カメラリグ (CameraPivot を回す・動かすスクリプト) の Script Execution Order。
    /// <para>
    /// CinemachineBrain は LateUpdate で VirtualCamera の Transform を Camera.main へ写す。
    /// Brain の実行順は既定 (0) で、シーンに元からある Brain は後から Spawn されたプレイヤーの
    /// スクリプトより先に LateUpdate が呼ばれる。そのためリグの回転・追従を既定順の LateUpdate で
    /// 行うと、Brain が 1 フレーム前の姿勢を写してしまう。
    /// リグ側を負の実行順にして、必ず Brain より前に姿勢を確定させる。
    /// </para>
    /// </summary>
    public static class CameraRigExecutionOrder
    {
        /// <summary> CinemachineBrain (0) より前、Fusion の Render (Update 段) より後 </summary>
        public const int Rig = -100;
    }
}
