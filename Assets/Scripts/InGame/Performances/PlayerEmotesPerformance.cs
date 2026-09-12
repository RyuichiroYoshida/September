using System;
using System.Collections.Generic;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using September.Common;
using Unity.Cinemachine;
using UnityEngine;

namespace September.InGame.Performances
{
    [Serializable]
    public class PlayerEmotesPerformance : IGameStartPerformance
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private CinemachineVirtualCamera _startCamera;
        [SerializeField] private Vector3 _cameraOffset;

        private static readonly string CueSheetName = "ALLCue";

        public bool Enabled => _enabled;

        /// <summary>
        /// ゲームスタート前にPlayerがポーズする
        /// </summary>
        public async UniTask RunPerformance(IGameStartPerformance.Context ctx)
        {
            PlayerDatabase playerDatabase = PlayerDatabase.Instance;
            CharacterDataContainer characterDataContainer = CharacterDataContainer.Instance;
            foreach (KeyValuePair<PlayerRef, NetworkObject> pair in playerDatabase.PlayerObjectDic)
            {
                NetworkObject player = pair.Value;
                AnimationClipPlayer animClipPlayer = player.GetComponent<AnimationClipPlayer>();
                CharacterType characterType = playerDatabase.PlayerDataDic[pair.Key].CharacterType;
                AnimationClip emoteClip = characterDataContainer.GetCharacterData(characterType).EmoteAnimation;
                _startCamera.transform.position = player.transform.position + player.transform.rotation * _cameraOffset;
                _startCamera.transform.rotation = player.transform.rotation;
                await UniTask.WaitForSeconds(1f);
                float delayTime = 1f;
                if (emoteClip)
                {
                    if (ctx.Runner.IsServer) animClipPlayer.PlayClip(emoteClip);
                    string cueName = characterDataContainer.GetCharacterData(characterType).StartVoice;
                    CRIAudio.PlaySE(CueSheetName, cueName); // ボイス呼び出し
                    delayTime = emoteClip.length;
                }

                await UniTask.WaitForSeconds(delayTime); // 各エモートのAnimation分待つ
            }

            _startCamera.Priority = -999;
        }
    }
}
