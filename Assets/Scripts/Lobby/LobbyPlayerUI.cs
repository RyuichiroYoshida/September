using Fusion;
using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Lobby
{
    public class LobbyPlayerUI : MonoBehaviour
    {
        [SerializeField, DisplayName("名前表示用テキスト(all)")] Text _nameText;
        [SerializeField, DisplayName("役職表示用テキスト(other)")] Text _jobText;
        [SerializeField, DisplayName("役職選択用ドロップダウン(self)")] Dropdown _dropdown;
        public Text NameText => _nameText;
        public Text JobText => _jobText;
        public Dropdown Dropdown => _dropdown;

        private void Awake()
        {
            if (_dropdown == null) return;
            foreach (var displayName in CharacterDataContainer.Instance.GetNames())
            {
                _dropdown.options.Add(new Dropdown.OptionData(displayName, null));
            }
        }
    }
}