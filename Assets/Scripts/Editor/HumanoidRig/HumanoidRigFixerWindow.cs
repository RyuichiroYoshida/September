#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// リグ作業ツールの EditorWindow。
    /// 共有の対象フォルダ設定とタブ切り替えのみを持ち、各タブの中身は Section クラスに委譲する。
    /// </summary>
    internal sealed class HumanoidRigFixerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Rig/Humanoid Rig Fixer";

        private enum Tab
        {
            Rig = 0,
            AvatarMask = 1,
            Consistency = 2,
            RootMotion = 3,
        }

        private static readonly string[] TabLabels = { "リグ検査/修正", "AvatarMask 一括適用", "整合性チェック", "Root Motion Node" };

        private HumanoidRigTargetFolders _folders;
        private TargetFolderListView _folderView;
        private RigDiagnosticsSection _rigSection;
        private AvatarMaskSection _maskSection;
        private ModelConsistencySection _consistencySection;
        private RootMotionNodeSection _rootMotionSection;
        private Tab _tab = Tab.Rig;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<HumanoidRigFixerWindow>("Humanoid Rig Fixer");
            window.minSize = new Vector2(720f, 420f);
        }

        private void OnEnable()
        {
            _folders = HumanoidRigTargetFolders.Load();
            _folderView = new TargetFolderListView(_folders);
            _rigSection = new RigDiagnosticsSection(_folders);
            _maskSection = new AvatarMaskSection(_folders);
            _consistencySection = new ModelConsistencySection(_folders);
            _rootMotionSection = new RootMotionNodeSection(_folders);
        }

        private void OnGUI()
        {
            _folderView.Draw();
            EditorGUILayout.Space();

            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels);
            EditorGUILayout.Space();

            switch (_tab)
            {
                case Tab.RootMotion:
                    _rootMotionSection.Draw();
                    break;
                case Tab.AvatarMask:
                    _maskSection.Draw();
                    break;
                case Tab.Consistency:
                    _consistencySection.Draw();
                    break;
                default:
                    _rigSection.Draw();
                    break;
            }
        }
    }
}
#endif
