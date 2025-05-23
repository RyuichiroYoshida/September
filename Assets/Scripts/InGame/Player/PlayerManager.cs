using System;
using Cinemachine;
using Fusion;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour
    {
        [SerializeField] PlayerStatus _playerStatus;
        
        public PlayerStatus PlayerStatus => _playerStatus;
        public bool IsLocalPlayer => HasInputAuthority;

        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                // カーソルを消す todo:ゲームロジックがやるべき
                // Cursor.visible = false;
                // Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
}
