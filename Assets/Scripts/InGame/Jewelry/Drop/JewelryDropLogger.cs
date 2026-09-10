using System.Diagnostics;
using System.Text;
using InGame.Jewelry.Common;
using September.InGame.Jewelry.Drop.Strategies.Amounts;
using September.InGame.Jewelry.Drop.Strategies.Chances;
using Debug = UnityEngine.Debug;

namespace September.InGame.Jewelry.Drop
{
    public static class JewelryDropLogger
    {
        private static readonly StringBuilder SettingsSb = new();
        private static readonly StringBuilder SectionSb = new();

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void JoinSettingsLog(JewelryType jewelryType, int dropAmount)
        {
            if (SettingsSb.Length == 0)
            {
                SettingsSb.AppendLine("[JewelryDropLogger] log:\n");
            }
            SettingsSb.AppendLine($"jewelryType:{jewelryType}, resultAmount:{dropAmount}");
            SettingsSb.AppendLine(SectionSb.ToString());
            SectionSb.Clear();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void StartSection(string sectionName)
        {
            SectionSb.AppendLine($"[{sectionName}]");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void AppendLog(IJewelryDropChance chance, float chanceAmount)
        {
            SectionSb.AppendLine($"- class:{chance.GetType().Name}, amount:{chanceAmount}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void AppendLog(IJewelryDropAmount drop, int dropAmount)
        {
            SectionSb.AppendLine($"- class:{drop.GetType().Name}, amount:{dropAmount}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void AppendLog(string message)
        {
            SectionSb.AppendLine(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEBUG")]
        public static void OutputLog()
        {
            Debug.Log(SettingsSb.ToString());
            SettingsSb.Clear();
            SectionSb.Clear();
        }
    }
}
