#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// モデル階層の Transform 名を HumanoidBoneNameRules で解釈し、HumanBodyBones への割当を推定する。
    /// AssetDatabase / ModelImporter には触れない純粋な推定ロジック (適用は HumanoidBoneMappingApplier)。
    /// </summary>
    internal static class HumanoidBoneNameMapper
    {
        private static readonly Regex FingerPattern =
            new Regex(@"^(?:hand)?f?(thumb|index|middle|ring|pinky|little)(?:finger)?0?(\d)$", RegexOptions.Compiled);
        private static readonly Regex SpinePattern = new Regex(@"^spine0*(\d*)$", RegexOptions.Compiled);
        private static readonly Regex SeparatorPattern = new Regex(@"[^a-z0-9]", RegexOptions.Compiled);
        private static readonly Regex FullSidePrefix = new Regex(@"^(left|right)[ _.\-]*(.+)$", RegexOptions.Compiled);
        private static readonly Regex FullSideSuffix = new Regex(@"^(.+?)[ _.\-]*(left|right)$", RegexOptions.Compiled);
        private static readonly Regex AbbrSidePrefix = new Regex(@"^([lr])[ _.\-]+(.+)$", RegexOptions.Compiled);
        private static readonly Regex AbbrSideSuffix = new Regex(@"^(.+?)[ _.\-]+([lr])$", RegexOptions.Compiled);
        private static readonly Regex GluedSidePrefix = new Regex(@"^([lr])([a-z].+)$", RegexOptions.Compiled);

        private const int SpineChainLength = 3; // Spine / Chest / UpperChest
        private const int MinPhalanx = 1;
        private const int MaxPhalanx = 3;

        public static HumanoidBoneNameMapping Map(Transform root)
        {
            var mapping = new HumanoidBoneNameMapping();
            var spineChain = new List<(int order, Transform bone)>();

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;

                if (TryParseSpine(t.name, out int spineOrder))
                {
                    spineChain.Add((spineOrder, t));
                    continue;
                }

                if (TryResolve(t.name, out HumanBodyBones bone))
                {
                    mapping.Assign(bone, t);
                }
                else
                {
                    mapping.Unmapped.Add(t);
                }
            }

            AssignSpineChain(mapping, spineChain);
            return mapping;
        }

        private static void AssignSpineChain(HumanoidBoneNameMapping mapping, List<(int order, Transform bone)> chain)
        {
            var ordered = chain.OrderBy(c => c.order).ToList();
            var targets = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i < SpineChainLength)
                {
                    mapping.Assign(targets[i], ordered[i].bone);
                }
                else
                {
                    // 4 節以上ある脊椎は Humanoid では表現できないので余りは未割当として報告する。
                    mapping.Unmapped.Add(ordered[i].bone);
                }
            }
        }

        private static bool TryParseSpine(string rawName, out int order)
        {
            order = 0;
            foreach (var (side, core) in EnumerateCandidates(rawName))
            {
                if (side != BoneSide.None) continue;
                var m = SpinePattern.Match(core);
                if (!m.Success) continue;
                order = m.Groups[1].Value.Length == 0 ? 0 : int.Parse(m.Groups[1].Value);
                return true;
            }
            return false;
        }

        private static bool TryResolve(string rawName, out HumanBodyBones bone)
        {
            foreach (var (side, core) in EnumerateCandidates(rawName))
            {
                if (TryResolveCore(side, core, out bone)) return true;
            }
            bone = HumanBodyBones.LastBone;
            return false;
        }

        private static bool TryResolveCore(BoneSide side, string core, out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;
            if (core.Length == 0) return false;

            if (side == BoneSide.None)
            {
                return HumanoidBoneNameRules.CenterAliases.TryGetValue(core, out bone);
            }

            if (HumanoidBoneNameRules.SidedAliases.TryGetValue(core, out var sided))
            {
                bone = HumanoidBoneNameRules.Resolve(sided, side);
                return true;
            }

            var finger = FingerPattern.Match(core);
            if (finger.Success && HumanoidBoneNameRules.FingerProximal.TryGetValue(finger.Groups[1].Value, out var proximal))
            {
                int phalanx = int.Parse(finger.Groups[2].Value); // 1=近位 2=中間 3=遠位、4 以上は末端ダミー
                if (phalanx < MinPhalanx || phalanx > MaxPhalanx) return false;
                var basis = side == BoneSide.Left ? proximal.left : proximal.right;
                bone = basis + (phalanx - MinPhalanx);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 接頭辞除去後の名前から「左右トークンの取り方」の候補を列挙する。
        /// "leg" のように l で始まる語を誤って左と解釈しないよう、確実な形式から順に試し、
        /// 呼び出し側は最初に解決できた候補を採用する。
        /// </summary>
        private static IEnumerable<(BoneSide side, string core)> EnumerateCandidates(string rawName)
        {
            string name = StripPrefix(rawName.ToLowerInvariant()).Trim();

            yield return (BoneSide.None, Normalize(name));

            var m = FullSidePrefix.Match(name);
            if (m.Success) yield return (ToSide(m.Groups[1].Value), Normalize(m.Groups[2].Value));

            m = FullSideSuffix.Match(name);
            if (m.Success) yield return (ToSide(m.Groups[2].Value), Normalize(m.Groups[1].Value));

            m = AbbrSidePrefix.Match(name);
            if (m.Success) yield return (ToSide(m.Groups[1].Value), Normalize(m.Groups[2].Value));

            m = AbbrSideSuffix.Match(name);
            if (m.Success) yield return (ToSide(m.Groups[2].Value), Normalize(m.Groups[1].Value));

            // 区切り無しの "lhand" / "rfoot" 形式 (最も曖昧なので最後に試す)
            m = GluedSidePrefix.Match(name);
            if (m.Success) yield return (ToSide(m.Groups[1].Value), Normalize(m.Groups[2].Value));
        }

        private static string StripPrefix(string lower)
        {
            foreach (var prefix in HumanoidBoneNameRules.StripPrefixes)
            {
                if (lower.StartsWith(prefix)) return lower.Substring(prefix.Length);
            }
            return lower;
        }

        private static string Normalize(string s) => SeparatorPattern.Replace(s, string.Empty);

        private static BoneSide ToSide(string token) =>
            token == "left" || token == "l" ? BoneSide.Left : BoneSide.Right;
    }

    /// <summary>命名規則マッピングの推定結果。</summary>
    internal sealed class HumanoidBoneNameMapping
    {
        public Dictionary<HumanBodyBones, Transform> Assigned { get; } = new Dictionary<HumanBodyBones, Transform>();
        public List<(HumanBodyBones bone, Transform ignored)> Conflicts { get; } = new List<(HumanBodyBones, Transform)>();
        public List<Transform> Unmapped { get; } = new List<Transform>();

        public IReadOnlyList<HumanBodyBones> MissingRequired =>
            HumanoidRequiredBones.Required.Where(b => !Assigned.ContainsKey(b)).ToList();

        public void Assign(HumanBodyBones bone, Transform t)
        {
            if (Assigned.ContainsKey(bone))
            {
                // 階層順で先に見つかった方を採用し、後続は競合として報告する。
                Conflicts.Add((bone, t));
                return;
            }
            Assigned[bone] = t;
        }

        public string Summarize()
        {
            var lines = new List<string> { $"割当 {Assigned.Count} 本 / 競合 {Conflicts.Count} / 未割当 {Unmapped.Count}" };
            foreach (var missing in MissingRequired) lines.Add($"  必須ボーン欠落: {missing}");
            foreach (var (bone, ignored) in Conflicts) lines.Add($"  競合 (無視): {bone} <- {ignored.name}");
            return string.Join("\n", lines);
        }
    }
}
#endif
