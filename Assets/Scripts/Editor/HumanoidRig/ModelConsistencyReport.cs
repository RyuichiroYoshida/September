#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

namespace September.Editor.HumanoidRig
{
    /// <summary>基準モデルとアニメーション FBX の整合性チェック結果 (データのみ)。</summary>
    internal sealed class ModelConsistencyReport
    {
        public string AssetPath { get; }
        public int ClipCount { get; set; }

        /// <summary>基準モデルにあるがアニメーション FBX に無いボーン名。</summary>
        public List<string> MissingBones { get; } = new List<string>();

        /// <summary>アニメーション FBX にのみ存在するボーン名。</summary>
        public List<string> ExtraBones { get; } = new List<string>();

        /// <summary>両方にあるが階層上の位置が異なるボーン。</summary>
        public List<string> MismatchedPaths { get; } = new List<string>();

        /// <summary>アニメーションカーブが指しているのに基準モデルに存在しないパス。</summary>
        public List<string> UnresolvedCurvePaths { get; } = new List<string>();

        /// <summary>致命的ではないが伝えるべき事項 (Animation Type の違い等)。</summary>
        public List<string> Notes { get; } = new List<string>();

        public ModelConsistencyReport(string assetPath)
        {
            AssetPath = assetPath;
        }

        /// <summary>そのままでは再生時にカーブが解決できず、リターゲットが破綻する状態。</summary>
        public bool HasError => UnresolvedCurvePaths.Count > 0 || MissingBones.Count > 0;

        public bool HasWarning => MismatchedPaths.Count > 0 || Notes.Count > 0;

        public string StatusLabel => HasError ? "不整合" : HasWarning ? "警告" : "OK";

        public string Summary
        {
            get
            {
                var sb = new StringBuilder();
                Append(sb, "カーブ解決不可", UnresolvedCurvePaths);
                Append(sb, "基準にあるが欠落", MissingBones);
                Append(sb, "階層位置が相違", MismatchedPaths);
                Append(sb, "基準に無い余剰ボーン", ExtraBones);
                foreach (var note in Notes) sb.AppendLine(note);
                if (sb.Length == 0) sb.Append($"クリップ {ClipCount} 件 / 整合");
                return sb.ToString().TrimEnd();
            }
        }

        private static void Append(StringBuilder sb, string label, List<string> values)
        {
            if (values.Count == 0) return;
            const int previewCount = 8;
            var preview = values.Count <= previewCount ? values : values.GetRange(0, previewCount);
            sb.Append(label).Append(" (").Append(values.Count).Append("): ").Append(string.Join(", ", preview));
            if (values.Count > previewCount) sb.Append(" ...");
            sb.AppendLine();
        }
    }
}
#endif
