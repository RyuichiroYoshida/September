using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using September.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    /// <summary>
    /// タイトル画面でのルーム作成・参加フローを管理する。
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        private const string NickNamePrompt = "プレイヤーの\n名前を決めてください";
        private const string NickNameRequiredMessage = "プレイヤー名を入力してください";

        [Header("Create Lobby")]
        [SerializeField] TMP_InputField _createLobbyName;
        [SerializeField] Slider _maxPlayers;
        [SerializeField] TextMeshProUGUI _selectedMapText;
        [SerializeField] TMP_Dropdown _mapTypeDropdown;
        [SerializeField] MapType _selectedMapType = MapType.Pirate;

        [Header("Join Lobby")]
        [SerializeField] TMP_InputField _joinLobbyName;

        [Header("Lobby Entry")]
        [SerializeField] TitlePanelController _titlePanelController;
        [SerializeField] NickNameSetter _nickNameSetter;
        [SerializeField] TextMeshProUGUI _nickNameMessageText;

        [Header("Error Message")]
        [SerializeField] TextMeshProUGUI _createMessageText;
        [SerializeField] TextMeshProUGUI _joinMessageText;
        [SerializeField] RoomErrorMessage _roomErrorMessage;

        private MapType[] _selectableMapTypes;
        private PendingLobbyOperation _pendingLobbyOperation;
        private string _pendingLobbyName;
        private int _pendingMaxPlayers;
        private MapType _pendingMapType;
        private bool _isProcessingLobbyEntry;

        public void Start()
        {
            _createMessageText?.gameObject.SetActive(false);
            _joinMessageText?.gameObject.SetActive(false);
            _selectableMapTypes = (MapType[])Enum.GetValues(typeof(MapType));
            InitializeMapTypeDropdown();
            UpdateSelectedMapText();
        }

        private void OnDestroy()
        {
            _mapTypeDropdown?.onValueChanged.RemoveListener(SelectMap);
        }

        /// <summary>
        /// 入力されたルーム作成設定を保持し、プレイヤー名入力へ進む。
        /// </summary>
        public void CreateLobby()
        {
            string lobbyName = _createLobbyName.text.Trim();
            if (string.IsNullOrEmpty(lobbyName)) return;

            _pendingLobbyOperation = PendingLobbyOperation.Create;
            _pendingLobbyName = lobbyName;
            _pendingMaxPlayers = (int)_maxPlayers.value;
            _pendingMapType = _selectedMapType;
            ShowNickNamePanel();
        }

        /// <summary>
        /// 入力された参加先ルーム名を保持し、プレイヤー名入力へ進む。
        /// </summary>
        public void JoinLobby()
        {
            string lobbyName = _joinLobbyName.text.Trim();
            if (string.IsNullOrEmpty(lobbyName)) return;

            _pendingLobbyOperation = PendingLobbyOperation.Join;
            _pendingLobbyName = lobbyName;
            ShowNickNamePanel();
        }

        /// <summary>
        /// 作成するルームのMapを次の候補へ切り替える。
        /// </summary>
        public void SelectNextMap()
        {
            if (_selectableMapTypes == null || _selectableMapTypes.Length == 0)
            {
                _selectableMapTypes = (MapType[])Enum.GetValues(typeof(MapType));
            }

            int currentIndex = Array.IndexOf(_selectableMapTypes, _selectedMapType);
            int nextIndex = (currentIndex + 1) % _selectableMapTypes.Length;
            SelectMap(nextIndex);
        }

        /// <summary>
        /// 指定された選択肢のMapをルーム作成対象に設定する。
        /// </summary>
        /// <param name="mapIndex">Map候補内の選択位置。</param>
        public void SelectMap(int mapIndex)
        {
            if (_selectableMapTypes == null || mapIndex < 0 || mapIndex >= _selectableMapTypes.Length) return;

            _selectedMapType = _selectableMapTypes[mapIndex];
            UpdateSelectedMapText();
        }

        /// <summary>
        /// プレイヤー名を確定し、保留中のルーム作成または参加を実行する。
        /// </summary>
        public async void ConfirmLobbyEntry()
        {
            if (_isProcessingLobbyEntry) return;

            if (_nickNameSetter == null || !_nickNameSetter.TryApplyNickName())
            {
                if (_nickNameMessageText != null)
                {
                    _nickNameMessageText.text = NickNameRequiredMessage;
                }

                return;
            }

            if (_pendingLobbyOperation == PendingLobbyOperation.None)
            {
                _titlePanelController?.ShowMainMenu();
                return;
            }

            _isProcessingLobbyEntry = true;

            try
            {
                if (_pendingLobbyOperation == PendingLobbyOperation.Create)
                {
                    await CreatePendingLobbyAsync();
                }
                else
                {
                    await JoinPendingLobbyAsync();
                }
            }
            finally
            {
                _isProcessingLobbyEntry = false;
            }
        }

        /// <summary>
        /// 名前入力を中止し、直前のルーム設定画面へ戻る。
        /// </summary>
        public void CancelLobbyEntry()
        {
            PendingLobbyOperation canceledOperation = _pendingLobbyOperation;
            ResetPendingLobbyEntry();

            if (canceledOperation == PendingLobbyOperation.Create)
            {
                _titlePanelController?.ShowHostRoom();
            }
            else if (canceledOperation == PendingLobbyOperation.Join)
            {
                _titlePanelController?.ShowJoinRoom();
            }
            else
            {
                _titlePanelController?.ShowMainMenu();
            }
        }

        /// <summary>
        /// StartGame�̌��ʂɉ����ăG���[���b�Z�[�W��ύX����
        /// </summary>
        /// <param name="result">StartGame�̌���</param>
        /// <param name="text">�G���[���b�Z�[�W��\������Text</param>
        private void ChangeErrorMessage(StartGameResult result, TextMeshProUGUI text)
        {
            if (result == null || text == null || _roomErrorMessage == null) return;

            if (result.Ok)
            {
                text.gameObject.SetActive(false);
                return;
            }
            text.gameObject.SetActive(true);
            text.text = _roomErrorMessage.GetMessage(result.ShutdownReason);
        }

        private void ShowNickNamePanel()
        {
            if (_nickNameMessageText != null)
            {
                _nickNameMessageText.text = NickNamePrompt;
            }

            _titlePanelController?.ShowUserProfile();
        }

        private void InitializeMapTypeDropdown()
        {
            if (_mapTypeDropdown == null) return;

            var mapNames = new List<string>(_selectableMapTypes.Length);
            foreach (MapType mapType in _selectableMapTypes)
            {
                mapNames.Add(GetMapDisplayName(mapType));
            }

            _mapTypeDropdown.ClearOptions();
            _mapTypeDropdown.AddOptions(mapNames);

            int selectedMapIndex = Array.IndexOf(_selectableMapTypes, _selectedMapType);
            _mapTypeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedMapIndex));
            _mapTypeDropdown.RefreshShownValue();
            _mapTypeDropdown.onValueChanged.AddListener(SelectMap);
        }

        private async UniTask CreatePendingLobbyAsync()
        {
            StartGameResult result = await NetworkManager.Instance.CreateLobby(
                _pendingLobbyName,
                _pendingMaxPlayers,
                _pendingMapType);
            ChangeErrorMessage(result, _createMessageText);

            if (result == null || !result.Ok)
            {
                ResetPendingLobbyEntry();
                _titlePanelController?.ShowHostRoom();
                return;
            }

            ResetPendingLobbyEntry();
            await NetworkManager.Instance.LoadLobbyScene();
        }

        private async UniTask JoinPendingLobbyAsync()
        {
            StartGameResult result = await NetworkManager.Instance.JoinLobby(_pendingLobbyName);
            ChangeErrorMessage(result, _joinMessageText);

            if (result == null || !result.Ok)
            {
                ResetPendingLobbyEntry();
                _titlePanelController?.ShowJoinRoom();
                return;
            }

            ResetPendingLobbyEntry();
        }

        private void UpdateSelectedMapText()
        {
            if (_selectedMapText == null) return;

            _selectedMapText.text = $"Map : {GetMapDisplayName(_selectedMapType)}";
        }

        private static string GetMapDisplayName(MapType mapType)
        {
            return mapType switch
            {
                MapType.Museum => "博物館",
                MapType.Pirate => "海賊船",
                _ => mapType.ToString()
            };
        }

        private void ResetPendingLobbyEntry()
        {
            _pendingLobbyOperation = PendingLobbyOperation.None;
            _pendingLobbyName = string.Empty;
            _pendingMaxPlayers = 0;
        }

        private enum PendingLobbyOperation
        {
            None,
            Create,
            Join
        }
    }
}
