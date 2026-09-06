using System;
using System.Reflection;

namespace Result
{
    public enum ExhibitType : byte
    {
        [DisplayName("なし")]
        None,
        [DisplayName("プテラ")]
        Ptr,
        [DisplayName("ティラノサウルス")]
        TRex,
        [DisplayName("アート")]
        Art,
        [DisplayName("飛行機")]
        AirPlane,
        [DisplayName("光学迷彩")]
        FlagealCamouflage,
        [DisplayName("ツタンカーメン")]
        Tutankhamun,
        [DisplayName("ロンドンテレフォン")]
        LondonTelephone,
        [DisplayName("車")]
        Car,
        [DisplayName("モアイ像")]
        Moai,
        [DisplayName("サテライトキャノン")]
        SateliteCanon,
        [DisplayName("楽器")]
        Instrument,
        [DisplayName("ムラマサ")]
        Muramasa,
        [DisplayName("マスト")]
        Mast,
        [DisplayName("武器庫")]
        Armory,
        [DisplayName("サメ")]
        Shark,
        [DisplayName("クラーケン")]
        Kraken,
        [DisplayName("海図")]
        NauticalChart,
        [DisplayName("ジップライン")]
        ZipLine,
        [DisplayName("バリスタ")]
        Ballista,
        [DisplayName("大砲")]
        Cannon,
    }

    [Serializable]
    public struct ExhibitScoreEntry
    {
        public ExhibitType Type;
        public int Points;
        public int DestroyPoints;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class DisplayNameAttribute : Attribute
    {
        public string Name { get; }
        public DisplayNameAttribute(string name) => Name = name;
    }

    public static class ExhibitTypeExtensions
    {
        public static string ToDisplayName(this ExhibitType type)
        {
            var field = type.GetType().GetField(type.ToString());
            var attr = field?.GetCustomAttribute<DisplayNameAttribute>();
            return attr?.Name ?? type.ToString();
        }
    }
}
