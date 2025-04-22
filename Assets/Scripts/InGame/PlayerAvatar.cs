using Fusion;
using September.Common;
using September.OgreSystem;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame
{
    public class PlayerAvatar : NetworkBehaviour
    {
        [SerializeField] Text _playerName;

        [Networked, OnChangedRender(nameof(OnNickNameChanged))]
        public NetworkString<_16> NickName { get; set; }

        [SerializeField] NetworkMecanimAnimator _networkAnimator;
        [SerializeField] Rigidbody _rigidbody;
        [SerializeField] private int _hp;

        [Networked] public PlayerData Data { get; set; }

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                Rpc_SetNickname(PlayerNetworkSettings.NickName);

                Data = new PlayerData(
                    id: Object.InputAuthority.PlayerId,
                    playerName: PlayerNetworkSettings.NickName,
                    maxHp: _hp,
                    currentHp: _hp,
                    isOgre: false,
                    isStunned: false,
                    playerRef: Object.InputAuthority
                );
                
                Debug.Log($"Player{Data.ID}のデータを登録完了");
                OgreManager.Instance.Register(Data);
                
                PlayerDatabase.Rpc_OnPlayerSpawned(Runner, this.Object, Runner.LocalPlayer);
            }
            else
            {
                _playerName.text = NickName.Value;
            }

            Runner.SetPlayerObject(Runner.LocalPlayer, Object);
        }

        public override void FixedUpdateNetwork()
        {
            var animator = _networkAnimator.Animator;
            animator.SetFloat("Speed", _rigidbody.linearVelocity.magnitude);
        }

        //  Robot KyleのアニメーションイベントでOnFootstepという関数が呼ばれるのでエラーが出ないように関数定義
        public void OnFootstep()
        {
        }

        public void OnNickNameChanged()
        {
            _playerName.text = NickName.Value;
        }

        // Only required in host/server mode.
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetNickname(string nickname)
        {
            NickName = nickname;
        }
    }
}