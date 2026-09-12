using Fusion;
using InGame.Player.Ability;
using Result;
using UnityEngine;

namespace InGame.Player
{
    public class TakamuraMovement : PlayerMovement
    {
        [SerializeField] MimickingParams _mimickingParams;
        [SerializeField] RevealAttackParams _attackParams;

#if UNITY_EDITOR
        [Header("Gizmo確認用")]
        [SerializeField] ExhibitType _previewExhibitType = ExhibitType.None;
#endif

        [Networked, HideInInspector]
        public MimicryState CurrentMimicryState { get; set; }

        [Networked, HideInInspector]
        public ScanAbilityPhase CurrentAbilityPhase { get; set; }

        [Networked, HideInInspector]
        public ExhibitType CurrentExhibitType { get; set; } = ExhibitType.None;

        protected override float GetMoveMagnification()
        {
            // メソッドoverride前の計算結果を取得
            var result = base.GetMoveMagnification();

            // 現在の"擬態状態"に応じた移動速度倍率を取得し乗算
            result *= _mimickingParams.TryGetParams(CurrentMimicryState, out var param)
                && param != null                // パラメータクラスを正常に取得できたか
                ? param.SpeedMagnification      // パラメータクラスに定義されている移動速度倍率
                : 1;                            // 倍率なし

            return result;
        }

        /// <summary>
        /// 擬態解除攻撃の攻撃範囲を取得するメソッド
        /// </summary>
        /// <returns>擬態解除攻撃の攻撃範囲</returns>
        public float GetRevealAttackRadius()
        {
            if (_attackParams == null) return 0;

#if UNITY_EDITOR
            // Editor停止中は確認用に選択した展示物の攻撃範囲を使用する
            if (!Application.isPlaying)
            {
                return _attackParams.GetRadius(_previewExhibitType);
            }
#endif

            // NetworkObjectの生成前はNetworkedプロパティを参照できないためデフォルト値を使用する
            var exhibitType = Object != null
                ? CurrentExhibitType
                : ExhibitType.None;

            return _attackParams.GetRadius(exhibitType);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRevealAttackRadius();
        }

        /// <summary>
        /// 擬態解除攻撃の攻撃範囲を描画するメソッド
        /// </summary>
        void DrawRevealAttackRadius()
        {
            var radius = GetRevealAttackRadius();
            if (radius <= 0) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
