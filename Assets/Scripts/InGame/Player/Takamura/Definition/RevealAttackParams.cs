using Result;
using UnityEngine;

namespace InGame.Player.Ability
{
    [CreateAssetMenu(fileName = "RevealAttackParams", menuName = "Scriptable Objects/Player/Takamura/RevealAttackParams")]
    public class RevealAttackParams : ScriptableObject
    {
        [Header("デフォルトの攻撃範囲"), SerializeField] float _defaultRadius = 1;
        [SerializeField] SerializableDictionary<ExhibitType, float> _radiusDict;

        /// <summary>
        /// 展示物に応じた攻撃範囲を取得するメソッド
        /// </summary>
        /// <param name="type">展示物の種類</param>
        /// <returns>攻撃範囲</returns>
        public float GetRadius(ExhibitType type)
        {
            if (_radiusDict == null || !_radiusDict.ContainsKey(type))
                return _defaultRadius;

            return _radiusDict[type];
        }
    }
}
