using System;
using UnityEditor;

namespace September.OgerSystem
{
    public class PlayerDatabaseEditor : EditorWindow
    {
        private PlayerDatabase _database;

        [MenuItem("Tools/PlayerDatabaseEditor")]
        public static void Open()
        {
            GetWindow<PlayerDatabaseEditor>("PlayerDatabaseEditor");
        }

        private void OnEnable()
        {
            _database = PlayerDatabase.Instatnce;
        }
        
        private void OnGUI()
        {
            if (_database == null)
            {
                EditorGUILayout.HelpBox("PlayerDatabase が見つかりません", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("登録プレイヤー一覧", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var allPlayers = _database.GetAll();
            foreach (var player in allPlayers)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("ID: " + player.ID);
                EditorGUILayout.LabelField("プレイヤー名: " + player.PlayerName);
                EditorGUILayout.LabelField("HP: " + player.CurrentHp.ToString());
                EditorGUILayout.LabelField("鬼: " + player.IsOgre);
                EditorGUILayout.LabelField("気絶: " + player.IsStunned);
                EditorGUILayout.EndVertical();
            }
        }
        
        private void Update()
        {
            Repaint();
        }
    }
}