#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>モデルの Transform 階層を、アニメーションカーブと同じ「ルートからの相対パス」で扱う。</summary>
    internal static class ModelBoneHierarchy
    {
        /// <summary>ルート自身を除く全 Transform の相対パス。</summary>
        public static HashSet<string> CollectPaths(GameObject root)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root.transform) continue;
                paths.Add(AnimationUtility.CalculateTransformPath(transform, root.transform));
            }
            return paths;
        }

        /// <summary>パス集合をボーン名で引けるようにする (同名ボーンが複数あり得るため値は一覧)。</summary>
        public static Dictionary<string, List<string>> GroupByName(IEnumerable<string> paths)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                string name = LeafName(path);
                if (!map.TryGetValue(name, out var list))
                {
                    list = new List<string>();
                    map.Add(name, list);
                }
                list.Add(path);
            }
            return map;
        }

        public static string LeafName(string path)
        {
            int index = path.LastIndexOf('/');
            return index < 0 ? path : path.Substring(index + 1);
        }
    }
}
#endif
