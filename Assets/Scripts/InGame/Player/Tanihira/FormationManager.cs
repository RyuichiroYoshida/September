using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Ingame.Tanihira
{
    public class FormationManager : NetworkBehaviour
    {
        [Header("隊列の設定")]
        [SerializeField] private Transform _firstFormationTransform;
        [SerializeField] private float _formationOffset = 1.0f;
        [Header("ワープ設定")] 
        [SerializeField] private float _warpSerchDistance = 10.0f;
        [SerializeField] private float _outFieldWarpHeight = 100.0f;
        [SerializeField] private float _inFieldWarpHeight = 10.0f;
        [SerializeField] private float _underWarpHeight = 30.0f;
        [SerializeField] private float _exceptionSerchDistance = 5.0f;
        [SerializeField] private LayerMask _raycastMask;
        [SerializeField] private float _maxWarpHeight = 5.0f;
        private List<FriendBase> _friendsList = new List<FriendBase>();
        private List<FriendBase> _currentFriendsList = new List<FriendBase>();
        private float _warpDuration = 0.5f;
        private FriendOrder _friendOrder;
        private PlayerMovement _playerMovement;
        private bool _isPlayerAirborne;
        private bool _isExternalMovement;
        private bool _isWarpingFriends;
        private int _airborneTickCount;
        private int _groundedTickCount;

        private const int AirborneDetectionTicks = 2;
        private const int LandingDetectionTicks = 2;
        private const float LandingWarpSearchDistance = 3.0f;
        private const float LandingWarpVerticalTolerance = 1.5f;

        public List<FriendBase> CurrentFriendsList => _currentFriendsList;
        public List<FriendBase> FriendsList => _friendsList;

        /// <summary>
        /// プレイヤーが一定時間空中にいるかどうか
        /// </summary>
        public bool IsPlayerAirborne => _isPlayerAirborne;
        

        private void Start()
        {
            _playerMovement = GetComponent<PlayerMovement>();

            if (HasInputAuthority)
            {
                _friendOrder = GetComponent<FriendOrder>();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _playerMovement == null)
                return;

            if (_playerMovement.IsGroundNet)
            {
                _airborneTickCount = 0;

                if (!_isPlayerAirborne)
                {
                    _groundedTickCount = 0;
                    return;
                }

                // 着地直後は接地判定が揺れることがあるため、安定してから一度だけ復帰させる
                _groundedTickCount++;
                if (_groundedTickCount < LandingDetectionTicks)
                    return;

                _isPlayerAirborne = false;
                _groundedTickCount = 0;

                if (!_isExternalMovement)
                    WarpFriendsAfterLanding(_playerMovement.transform.position, _playerMovement.transform.rotation);

                return;
            }

            _groundedTickCount = 0;
            _airborneTickCount++;
            if (_airborneTickCount >= AirborneDetectionTicks)
                _isPlayerAirborne = true;
        }

        /// <summary>
        /// 友達登録処理
        /// </summary>
        /// <param name="friend"></param>
        public Transform Register(FriendBase friend)
        {
            //先頭の位置を返す
            if (_currentFriendsList.Count == 0)
            {
                _currentFriendsList.Add(friend);
                return _firstFormationTransform;
            }
            else //最後尾のオブジェクトのTransformを返す
            {
                Transform newDestination = _currentFriendsList.Last().FormationPos;
                _currentFriendsList.Add(friend);
                return newDestination;
            }
        }

        /// <summary>
        /// フレンドの隊列削除処理
        /// </summary>
        /// <param name="friend"></param>
        public void DeleteFriend(FriendBase friend)
        {
            if(!HasStateAuthority)
                return;
            
            int index = _currentFriendsList.IndexOf(friend);
            if (index >= 0)
            {
                _currentFriendsList.RemoveAt(index);
                SortFormation();
            }
        }

        /// <summary>
        /// ボスペンギンを返す
        /// </summary>
        public BossPenguinFriend GetBossFriend()
        {
            foreach (FriendBase friend in _currentFriendsList)
            {
                if(friend.TryGetComponent<BossPenguinFriend>(out BossPenguinFriend bossPenguinFriend))
                    return bossPenguinFriend;
            }
            
            return null;
        }

        /// <summary>
        /// 隊列整理
        /// </summary>
        public void SortFormation()
        {
            if(!HasStateAuthority)
                return;

            if (_currentFriendsList[0].CurrentState is FriendState.Chase or FriendState.Attack)
                return;
            
            if(_currentFriendsList.Count > 0)
            {
                for(int i = 0; i < _currentFriendsList.Count; i++)
                {
                    FriendBase friend = _currentFriendsList[i];

                    if (i == 0) //先頭の場合
                    {
                        friend.SetDestination(_firstFormationTransform);
                    }
                    else
                    {
                        friend.SetDestination(_currentFriendsList[i - 1].FormationPos);
                    }
                }
            }
        }

        /// <summary>
        /// 現在の隊列を登録する
        /// </summary>
        public void RegisterFriendFormation()
        {
            if(!HasStateAuthority)
                return;
            
            _friendsList.Clear();
            foreach (FriendBase friend in _currentFriendsList)
            {
                _friendsList.Add(friend);
            }
        }

        private FriendBase[] GetFriendsForWarp()
        {
            return _currentFriendsList.Where(friend => friend != null).ToArray();
        }

        /// <summary>
        /// プレイヤーの着地を待ってから、隊列のフレンドをプレイヤーの近くにワープさせる
        /// </summary>
        /// <param name="playerMovement">着地状態を確認するプレイヤーの移動コンポーネント</param>
        public void WarpFriendNearPlayerWhenGrounded(PlayerMovement playerMovement)
        {
            _isExternalMovement = false;
            WarpFriendNearPlayerWhenGroundedAsync(playerMovement).Forget();
        }

        private async UniTask WarpFriendNearPlayerWhenGroundedAsync(PlayerMovement playerMovement)
        {
            if (!HasStateAuthority || playerMovement == null)
                return;

            const int maxWaitTicks = 120;
            int groundedTickCount = 0;
            for (int i = 0; i < maxWaitTicks; i++)
            {
                if (playerMovement.IsGroundNet)
                {
                    groundedTickCount++;
                    if (groundedTickCount >= LandingDetectionTicks)
                        break;
                }
                else
                {
                    groundedTickCount = 0;
                }

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, this.GetCancellationTokenOnDestroy());
            }

            if (playerMovement == null || groundedTickCount < LandingDetectionTicks)
                return;

            WarpFriendsAfterLanding(playerMovement.transform.position, playerMovement.transform.rotation);
        }

        private void WarpFriendsAfterLanding(Vector3 warpPosition, Quaternion warpRotation)
        {
            _isExternalMovement = false;
            WarpFriendNearPlayerAsync(warpPosition, warpRotation, true).Forget();
        }

        /// <summary>
        /// 隊列にいるフレンドをプレイヤーの近くにワープさせる
        /// </summary>
        public void WarpFriendNearPlayer(Vector3 warpPosition, Quaternion warpRotation)
        {
            _isExternalMovement = false;
            WarpFriendNearPlayerAsync(warpPosition, warpRotation, false).Forget();
        }

        private async UniTask WarpFriendNearPlayerAsync(Vector3 warpPosition, Quaternion warpRotation,
            bool isLandingRecovery)
        {
            if (!HasStateAuthority || _isWarpingFriends)
                return;

            FriendBase[] friends = GetFriendsForWarp();
            if (friends.Length == 0)
                return;

            _isWarpingFriends = true;

            try
            {
                var validFriends = new List<FriendBase>();
                var warpPositions = new Dictionary<FriendBase, Vector3>();

                // 先にワープ先を確定し、失敗するフレンドのAgentやステートは変更しない。
                foreach (FriendBase friend in friends)
                {
                    if (friend.Agent == null
                        || !TryGetWarpPosition(warpPosition, friend.Agent.baseOffset,
                            isLandingRecovery, out Vector3 fixedPos))
                    {
                        Debug.LogWarning($"{friend.name}をNavMesh上へワープできませんでした", friend);
                        ResumeFriendAfterWarpSearchFailure(friend);
                        continue;
                    }

                    validFriends.Add(friend);
                    warpPositions.Add(friend, fixedPos);
                }

                if (validFriends.Count == 0)
                    return;

                var previousStates = new Dictionary<FriendBase, FriendState>();
                var previousPositions = new Dictionary<FriendBase, Vector3>();
                var previousRotations = new Dictionary<FriendBase, Quaternion>();
                var previousWarpStates = new Dictionary<FriendBase, bool>();

                // フレンドのステートを切り替え、移動だけを停止する。
                foreach (FriendBase friend in validFriends)
                {
                    previousStates[friend] = friend.CurrentState;
                    previousPositions[friend] = friend.transform.position;
                    previousRotations[friend] = friend.transform.rotation;
                    previousWarpStates[friend] = friend.IsWarp;

                    if (friend.Animator != null)
                        friend.Animator.SetFloat("MoveBlend", 0);

                    friend.IsWarp = false;
                    friend.ChangeState(FriendState.Wait);

                    if (friend.Agent.enabled && friend.Agent.isOnNavMesh)
                        friend.Agent.isStopped = true;
                }

                //少し待ってから移動させる
                await UniTask.Delay(TimeSpan.FromSeconds(_warpDuration),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
                
                // フレンドを隊列の位置へワープさせる
                foreach (FriendBase friend in validFriends)
                {
                    Vector3 fixedPos = warpPositions[friend];

                    // Agentを無効化せずにWarpする。無効化状態のフレンドはここでのみ再有効化する。
                    friend.transform.SetPositionAndRotation(fixedPos, warpRotation);
                    friend.Agent.enabled = true;
                    bool warped = friend.Agent.Warp(fixedPos);

                    if (!warped || !friend.Agent.isOnNavMesh)
                    {
                        friend.Agent.enabled = false;
                        friend.transform.SetPositionAndRotation(fixedPos, warpRotation);
                        friend.Agent.enabled = true;
                        warped = friend.Agent.Warp(fixedPos);
                    }

                    if (!warped || !friend.Agent.isOnNavMesh)
                    {
                        RestoreFriendAfterWarpFailure(
                            friend,
                            previousPositions[friend],
                            previousRotations[friend],
                            previousStates[friend],
                            previousWarpStates[friend]);
                        Debug.LogWarning($"{friend.name}のNavMeshAgentを復旧できませんでした", friend);
                        continue;
                    }

                    friend.Agent.nextPosition = fixedPos;
                    friend.Agent.isStopped = true;

                    var networkTransform = friend.GetComponent<NetworkTransform>();
                    networkTransform?.Teleport(fixedPos, warpRotation);
                }
            }
            finally
            {
                _isWarpingFriends = false;
            }
        }

        private void RestoreFriendAfterWarpFailure(
            FriendBase friend,
            Vector3 previousPosition,
            Quaternion previousRotation,
            FriendState previousState,
            bool previousIsWarp)
        {
            friend.transform.SetPositionAndRotation(previousPosition, previousRotation);
            friend.Agent.enabled = true;

            if (friend.Agent.isOnNavMesh)
            {
                friend.Agent.Warp(previousPosition);
                friend.Agent.nextPosition = previousPosition;
            }

            friend.IsWarp = false;
            friend.ChangeState(previousState);
            friend.IsWarp = previousIsWarp;

            if (friend.Agent.enabled && friend.Agent.isOnNavMesh)
                friend.Agent.isStopped = previousState is not (FriendState.Move or FriendState.Chase);
        }

        private static void ResumeFriendAfterWarpSearchFailure(FriendBase friend)
        {
            if (friend.Agent == null
                || !friend.Agent.enabled
                || !friend.Agent.isOnNavMesh
                || friend.CurrentState is not (FriendState.Move or FriendState.Chase))
            {
                return;
            }

            friend.Agent.isStopped = false;
        }

        private bool TryGetWarpPosition(Vector3 warpPosition, float agentBaseOffset, bool isLandingRecovery,
            out Vector3 fixedPos)
        {
            fixedPos = warpPosition;
            NavMeshHit hit;

            if (isLandingRecovery)
                return TryGetLandingWarpPosition(warpPosition, agentBaseOffset, out fixedPos);

            if (NavMesh.SamplePosition(warpPosition, out hit, _warpSerchDistance, NavMesh.AllAreas) &&
                hit.position.y < _maxWarpHeight)
            {
                fixedPos = hit.position + Vector3.up * agentBaseOffset;
                return true;
            }

            if (Physics.Raycast(warpPosition, Vector3.down, out RaycastHit raycastHit, _underWarpHeight,
                    _raycastMask) &&
                NavMesh.SamplePosition(raycastHit.point, out hit, _warpSerchDistance, NavMesh.AllAreas))
            {
                fixedPos = hit.position + Vector3.up * agentBaseOffset;
                return true;
            }

            var exceptionPosition = warpPosition;
            exceptionPosition.y = _inFieldWarpHeight;
            if (NavMesh.SamplePosition(exceptionPosition, out hit, _exceptionSerchDistance, NavMesh.AllAreas))
            {
                fixedPos = hit.position + Vector3.up * agentBaseOffset;
                return true;
            }

            return false;
        }

        /// <summary>
        /// プレイヤーが実際に着地した地面を基準に、フレンドのワープ位置を取得する
        /// </summary>
        private bool TryGetLandingWarpPosition(Vector3 warpPosition, float agentBaseOffset, out Vector3 fixedPos)
        {
            fixedPos = warpPosition;

            // プレイヤー直下の地面を優先することで、上下に離れたNavMeshの孤島を拾わない
            Vector3 rayOrigin = warpPosition + Vector3.up * 0.5f;
            float rayDistance = _underWarpHeight + 0.5f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, rayDistance, _raycastMask) &&
                NavMesh.SamplePosition(groundHit.point, out NavMeshHit navMeshHit, LandingWarpSearchDistance,
                    NavMesh.AllAreas) &&
                Mathf.Abs(navMeshHit.position.y - groundHit.point.y) <= LandingWarpVerticalTolerance)
            {
                fixedPos = navMeshHit.position + Vector3.up * agentBaseOffset;
                return true;
            }

            // 地面レイヤーを取得できない場所では、着地点の近傍だけを検索する
            if (NavMesh.SamplePosition(warpPosition, out navMeshHit, LandingWarpSearchDistance, NavMesh.AllAreas) &&
                Mathf.Abs(navMeshHit.position.y - warpPosition.y) <= LandingWarpVerticalTolerance)
            {
                fixedPos = navMeshHit.position + Vector3.up * agentBaseOffset;
                return true;
            }

            return false;
        }

        [ContextMenu("FriendWarpOutSide")]
        public async void WarpFriendOutField()
        {
            if(!HasStateAuthority)
                return;

            _isExternalMovement = true;

            FriendBase[] friends = GetFriendsForWarp();
            if (friends.Length == 0)
                return;
            
            //強制的にプレイヤーに追従するようにする
            _friendOrder?.OrderReturnFriend();
            
            //フレンドのステートの切り替え
            foreach (FriendBase friend in friends)
            {
                if (friend == null)
                    continue;

                if (friend.Agent != null)
                    friend.Agent.enabled = false;

                friend.ChangeState(FriendState.None);
                friend.IsWarp = true;
            }
            
            //少し待ってから移動させる
            await UniTask.Delay(TimeSpan.FromSeconds(_warpDuration),
                cancellationToken: this.GetCancellationTokenOnDestroy());
            
            Vector3 warpPos = new Vector3(0, _outFieldWarpHeight , 0);
            //フレンドを全員ワープさせる
            foreach (FriendBase friend in friends)
            {
                if (friend == null)
                    continue;

                var networkTransform = friend.GetComponent<NetworkTransform>();
                networkTransform?.Teleport(warpPos);
            }
        }
        
        /// <summary>
        /// 隊列のペンギンに攻撃指示状態を設定する
        /// </summary>
        /// <param name="isOrdered">攻撃指示を行う場合はtrue</param>
        public void SetAllAttackOrdered(bool isOrdered)
        {
            foreach (var friend in CurrentFriendsList)
            {
                friend.SetAttackOrdered(isOrdered);
            }
        }
        
#if UNITY_EDITOR       
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 1, 0.5f); // 半透明
            Vector3 startPos = new Vector3(0, _inFieldWarpHeight, 0);
            Gizmos.DrawWireCube(startPos, new Vector3(10, 1, 10));
            
            Gizmos.color = new Color(1, 1, 1, 0.5f); // 半透明
            Vector3 maxWarpstartPos = new Vector3(0, _maxWarpHeight, 0);
            Gizmos.DrawWireCube(maxWarpstartPos, new Vector3(10, 1, 10));
        }
#endif
    }
}
