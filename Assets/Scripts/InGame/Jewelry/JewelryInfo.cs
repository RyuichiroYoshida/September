using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Jewelry.Common
{
    /// <summary>宝石の定義を持つクラス</summary>
    [CreateAssetMenu(fileName = "JewelryInfo", menuName = "Jewelry/JewelryInfo")]
    public class JewelryInfo : ScriptableObject
    {
        [Header("宝石の種類"), SerializeField] JewelryType _jewelryType;
        [Header("獲得した時のスコア"), SerializeField] int _score = 1;
        [Header("所持UIとして表示するときのアイコン")]
        [FormerlySerializedAs("_jewelrySprite")]
        [SerializeField] private Sprite _jewelryUISprite;

        [Header("プレイヤー頭上に表示するときのアイコン")]
        [SerializeField] private Sprite _jewelryOverheadSprite;

        [Header("デスポーン設定")]
        [Tooltip("自動消滅までの時間"), SerializeField] private float _lifeTime = 30f;
        [Tooltip("点滅開始残り時間"), SerializeField] private float _blinkStartRemainingTime = 5f;
        [Tooltip("点滅回数(毎秒)"), SerializeField] private float _blinkSpeed = 5f;
        [Tooltip("海に落ちた際にデスポーンする深さ"), SerializeField] private float _fallDepth = 10f;

        [Header("Effect")]
        [SerializeField] private EffectType _pickupEffectType;
        [SerializeField] private Vector3 _pickupEffectOffset;

        public JewelryType JewelryType => _jewelryType;
        public int Score => _score;
        public Sprite JewelryUISprite => _jewelryUISprite;
        public Sprite JewelryOverheadSprite =>
            _jewelryOverheadSprite != null
                ? _jewelryOverheadSprite
                : _jewelryUISprite;

        // 既存コードとの互換性を維持する。
        public Sprite JewelrySprite => JewelryUISprite;
        public float LifeTime => _lifeTime;
        public float BlinkStartRemainingTime => _blinkStartRemainingTime;
        public float BlinkSpeed => _blinkSpeed;
        public float FallDepth => _fallDepth;
        public EffectType PickupEffectType => _pickupEffectType;
        public Vector3 PickupEffectOffset => _pickupEffectOffset;
    }
}
