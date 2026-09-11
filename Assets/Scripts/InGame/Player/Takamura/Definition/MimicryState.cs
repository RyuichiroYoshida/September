using UnityEngine;

namespace InGame.Player
{
    /// <summary>擬態状態を定義したenum</summary>
    public enum MimicryState
    {
        [InspectorName("擬態していない")] Default = 0,
        [InspectorName("展示物に擬態")] MimicExhibit = 1,
        [InspectorName("キャラクターに擬態")] MimicCharacter = 2,
    }
}
