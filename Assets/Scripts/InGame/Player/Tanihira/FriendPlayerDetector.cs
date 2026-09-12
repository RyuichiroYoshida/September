using System.Collections.Generic;
using System.Linq;
using Fusion;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace Ingame.Tanihira
{
    public class FriendPlayerDetector : NetworkBehaviour
    {
        [SerializeField] private Transform _detectionCenter;
        [SerializeField] private float _detectionRadius;
        [SerializeField] private LayerMask _detectionMask;
        [SerializeField] private LayerMask _obstacleMask;
        [SerializeField] private FriendStateChanger _friendStateChanger;
        [SerializeField] private FormationManager _formationManager;
        [SerializeField] private FriendOrder _friendOrder;
        [SerializeField] private bool _isWaiting;
        
        private Transform _currentTarget;
        private InGameManager _inGameManager;
        [SerializeField, ReadOnly] private List<TyrannoInteractable> _tyrannoInteractables = new List<TyrannoInteractable>();
        private Collider[] _overlapResults = new Collider[50];
        private bool _isEnd;

        [SerializeField, ReadOnly] private bool _isSerching = true;
        private Transform _moveIndependentDestination; //ペンギンが自立する時の目的地
        private BossPenguinFriend _bossPenguinFriend;

        private void Start()
        {
            if (HasStateAuthority)
            {
                _inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (_inGameManager)
                {
                    _inGameManager.GameStarted += GameStart;
                }
                else
                {
                    Debug.LogError("InGameManagerが見つかりません");
                }
                _tyrannoInteractables = FindObjectsOfType<TyrannoInteractable>().ToList();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            if (_isWaiting && !_isEnd)
            {
                DetectePlayer();
            }
            
            //終わりか動かをチェックする
            if (_inGameManager?.CurrentStateName == "EndingState" && !_isEnd)
            {
                _isEnd = true;
                if (_formationManager)
                {
                    foreach (var friend in _formationManager.FriendsList)
                    {
                        friend.EndFriend();
                    }
                }
            }
        }

        /// <summary>
        /// 索敵の中心を変更する
        /// </summary>
        /// <param name="target"></param>
        public void ChangeDetectionCenter(Transform target, bool isSerching, Transform moveDestination = null)
        {
            _detectionCenter = target;
            _isSerching = isSerching;
            _moveIndependentDestination = moveDestination;
        }
        
        private void GameStart()
        {
            _isWaiting = true;
        }

        //プレイヤーを索敵する処理
        private void DetectePlayer()
        {
            if (_formationManager.CurrentFriendsList.FirstOrDefault() is { CurrentState: FriendState.None })
                return;

            // ターゲットが無効だったら再索敵
            if (!IsTargetValid(_currentTarget))
            {
                _currentTarget = null;
                
                //範囲内のプレイヤーを検出
                int hitPlayerCount = Physics.OverlapSphereNonAlloc(_detectionCenter.position, _detectionRadius, _overlapResults, _detectionMask);
                var players = _overlapResults.Take(hitPlayerCount)
                    .Select(col => col.transform.root) // ルート Transform を抽出
                    .Where(root => root != transform.root) // 自分自身は除外
                    .Distinct();                                            // 重複排除
                
                //ティラノをチェック
                var tyrannos = _tyrannoInteractables
                    .Where(t => t != null && t.IsInteracting 
                                          && (t.transform.position - _detectionCenter.position).sqrMagnitude <= _detectionRadius * _detectionRadius)
                    .Select(t => t.transform);
                
                //近い順にソート
                var uniqueRoots = players
                    .Concat(tyrannos)
                    .Distinct()
                    .OrderBy(root => (root.position - _detectionCenter.position).sqrMagnitude)
                    .ToList();
                
                foreach (Transform player in uniqueRoots)
                {
                    //自身は除く
                    if (player.gameObject == gameObject)
                        continue;
                    
                    // Rayの始点を少し上に
                    Vector3 start = _detectionCenter.position + Vector3.up * 0.5f;

                    // ターゲットの中心も少し上に
                    Vector3 targetCenter = player.position + Vector3.up * 0.5f;

                    Vector3 direction = (targetCenter - start).normalized;
                    float distance = Vector3.Distance(start, targetCenter);
                    
                    //間に障害物があった場合には無視
                    if (Physics.Raycast(start, direction, out RaycastHit hit, distance, _obstacleMask))
                    {
                        //ティラノに当たった場合は無視をする
                        if (hit.collider.GetComponent<TyrannoInteractable>() != null)
                        {
                            //何もしない
                        }
                        else
                        {
                            continue;
                        }
                    }
                    
                    //近い物をターゲットにして攻撃させる
                    _currentTarget = player.gameObject.transform;
                    //近い順にソート済みなので最初に当たったものが最も近い
                    break;
                }
            }
            
            //ペンギンに攻撃指示を飛ばす
            if (_currentTarget)
            {
                bool flag = false;
                // 攻撃指示を受けているかを判定する
                foreach (var friend in _formationManager.CurrentFriendsList)
                {
                    if (friend.IsAttackOrdered)
                    {
                        flag = true;
                        break;
                    }
                }
                
                if (!flag)
                {
                    _friendStateChanger.SetMoveState();
                    return;
                }
                _friendStateChanger.SetChaseState(_currentTarget);
            }
            else
            {
                if (_isSerching)
                {
                    _friendStateChanger.SetMoveState();
                }
                else
                {
                    _friendStateChanger.IndependentFrienState(_moveIndependentDestination);
                }
                
            }
        }
        
        //ターゲットが視野角内にいるか
        private bool IsTargetValid(Transform target)
        {
            if (target == null) 
                return false;

            Vector3 direction = (target.position - _detectionCenter.position).normalized;
            float distance = Vector3.Distance(_detectionCenter.position, target.position);

            // 範囲外
            if (distance > _detectionRadius)
            {
                _currentTarget = null;
                return false;
            }

            // 視線が通らない
            if (Physics.Raycast(_detectionCenter.position, direction, distance, _obstacleMask))
            {
                _currentTarget = null;
                return false;
            }

            return true;
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_detectionCenter == null)
                return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // 赤色で半透明
            Gizmos.DrawWireSphere(_detectionCenter.position, _detectionRadius);
            Gizmos.DrawSphere(_detectionCenter.position, 0.05f); // 中心点のマーカー
        }
    }
}
