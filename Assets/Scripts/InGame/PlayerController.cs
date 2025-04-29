using System;
using System.Collections.Generic;
using Fusion;
using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame
{
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] PlayerData _playerData;
        [SerializeField] Text _playerNameDisplay;
        [SerializeField] Rigidbody _rigidbody;
        [SerializeField] Animator _animator;
        [SerializeField] PlayerCameraController _playerCameraController;
        [SerializeField] List<BasePlayerModule> _modules;
        
        [Networked, OnChangedRender(nameof(OnNickNameChanged)), HideInInspector]
        public NetworkString<_16> NickName { get; set; }
        [Networked, OnChangedRender(nameof(OnOgreChanged)), HideInInspector] 
        public NetworkBool IsOgre { get; set; }
        [Networked, OnChangedRender(nameof(OnHpChanged)), HideInInspector] 
        public int CurrentHp { get; set; }
        [Networked] 
        TickTimer StunTimer { get; set; }
        //  交代した2人に鬼が変わったことを通知する
        public Action OnOgreChangedAction { get; set; }
        public Action<int, int> OnHpChangedAction { get; set; }
        public PlayerData Data => _playerData;
        //  全員に鬼が交代したことを通知する
        public static Action OnOgreChangedRPC { get; set; }
        
        public override void Spawned()
        {
            if (HasInputAuthority)
            {
                Rpc_SetNickname(PlayerNetworkSettings.NickName);
            }
            else
            {
                _playerNameDisplay.text = NickName.Value;
            }
            CurrentHp = _playerData.HitPoint;
            foreach (var module in _modules)
            {
                module.Initialize(this, _playerCameraController, _playerData, _rigidbody, _animator);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (StunTimer.Expired(Runner))
            {
                _animator.SetBool("Stun", false);
                _rigidbody.isKinematic = false;
            }
            _animator.SetFloat("Speed", _rigidbody.linearVelocity.magnitude);
        }

        public void Stun()
        {
            if (!StunTimer.ExpiredOrNotRunning(Runner)) return;
            _rigidbody.isKinematic = true;
            _animator.SetBool("Stun", true);
            StunTimer = TickTimer.CreateFromSeconds(Runner, _playerData.StunTime);
        }
        /// <summary>
        /// ダメージ処理、HPが0以下になれば鬼を交代する
        /// </summary>
        public void TakeDamage(PlayerController attackerController, int damage)
        {
            //  攻撃者が鬼じゃないか対象が鬼なら処理を終わる
            if (!attackerController.IsOgre || IsOgre) return;
            
            // ダメージ計算
            CurrentHp -= damage;

            // 対象のhpが0より大きければ交代は発生しない
            if (CurrentHp > 0) return;
            
            CurrentHp = _playerData.HitPoint;
            attackerController.IsOgre = false;
            IsOgre = true;
            Rpc_OnOgreChanged();
            Stun();
        }

        public void OnOgreChanged() => OnOgreChangedAction?.Invoke();
        public void OnHpChanged() => OnHpChangedAction?.Invoke(CurrentHp, _playerData.HitPoint);
        public void OnNickNameChanged()
        {
            _playerNameDisplay.text = NickName.Value;
        }
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetNickname(string nickname)
        {
            NickName = nickname;
        }
        [Rpc]
        public static void Rpc_OnOgreChanged()
        {
            OnOgreChangedRPC?.Invoke();
        } 
        public void OnFootstep(){}
    }
}