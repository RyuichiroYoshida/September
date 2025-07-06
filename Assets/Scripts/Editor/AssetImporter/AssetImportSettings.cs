using UnityEditor;

namespace September.Editor.AssetImporter
{
    public class AssetImportSettings
    {
        private const string ShowImportWindowKey = "September.AssetImporter.ShowImportWindow";
        private const string UseLoggerKey = "September.AssetImporter.UseLogger";
        private const string FileCheckingKey = "September.AssetImporter.FileChecking";
        private const string SelectedReleaseIndexKey = "September.AssetImporter.SelectedReleaseIndex";

        public static bool ShowImportWindow
        {
            get => EditorPrefs.GetBool(ShowImportWindowKey, false);
            set => EditorPrefs.SetBool(ShowImportWindowKey, value);
        }

        public static bool UseLogger
        {
            get => EditorPrefs.GetBool(UseLoggerKey, true);
            set => EditorPrefs.SetBool(UseLoggerKey, value);
        }

        public static bool FileChecking
        {
            get => EditorPrefs.GetBool(FileCheckingKey, false);
            set => EditorPrefs.SetBool(FileCheckingKey, value);
        }

        public static int SelectedReleaseIndex
        {
            get => EditorPrefs.GetInt(SelectedReleaseIndexKey, 0);
            set => EditorPrefs.SetInt(SelectedReleaseIndexKey, value);
        }

        public static void ResetToDefaults()
        {
            EditorPrefs.DeleteKey(ShowImportWindowKey);
            EditorPrefs.DeleteKey(UseLoggerKey);
            EditorPrefs.DeleteKey(FileCheckingKey);
            EditorPrefs.DeleteKey(SelectedReleaseIndexKey);
        }
    }
}