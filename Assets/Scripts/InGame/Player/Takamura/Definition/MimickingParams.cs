using UnityEngine;
using System;
using System.Collections.Generic;

namespace InGame.Player
{
    /// <summary>擬態状態に応じたパラメータを定義したクラス</summary>
    [Serializable]
    public class MimickingParams
    {
        [Header("パラメータ群"), SerializeField] MimickingParam[] _mimickingParams;

        /// <summary>ステートとパラメータを対応させた辞書</summary>
        readonly Dictionary<MimicryState, MimickingParam> _mimickingParamsDic = new();

        /// <summary>
        /// ステートに応じたパラメータを取得するメソッド
        /// </summary>
        /// <param name="state">擬態状態</param>
        /// <param name="param">ステートに応じたパラメータ</param>
        /// <returns>パラメータを取得できたかどうか</returns>
        public bool TryGetParams(MimicryState state, out MimickingParam param)
        {
            param = null;

            // 配列の定義がなければ何もせず終了
            if (_mimickingParams == null) return false;

            // 辞書に何も登録されてなければ先に辞書の作成
            if (_mimickingParamsDic.Count <= 0)
            {
                foreach (var paramClass in _mimickingParams)
                {
                    if (paramClass != null)
                        _mimickingParamsDic[paramClass.MimicryState] = paramClass;
                }
            }

            return _mimickingParamsDic.TryGetValue(state, out param);
        }
    }

    [Serializable]
    public class MimickingParam
    {
        [Header("擬態状態"), SerializeField] MimicryState _mimicryState;
        [Header("移動速度倍率"), SerializeField] float _speedMagnification = 1f;

        public MimicryState MimicryState => _mimicryState;
        public float SpeedMagnification => _speedMagnification;
    }
}
