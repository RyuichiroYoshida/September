using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;

namespace InGame.Bot
{
    /// <summary>
    /// MapDataに書き込みをするクラス
    /// ゲーム実行中は動かさない
    /// </summary>
    public class NodeGanerator : MonoBehaviour
    {
        [SerializeField, Tooltip("生成したノードを書き込む")] private NodeMapData _mapData;

        [SerializeField, Tooltip("経路到達判定の基準地点")] private Transform _origin;
        [SerializeField, Tooltip("Origin への NavMesh 到達経路を必須とするか（孤立した足場やワープ地点にもノードを作る場合は false）")] private bool _requirePathToOrigin = false;
        [SerializeField, Tooltip("描画するか")] private bool _isDrawGizumoIcon;

        [Header("Poisson Disk")]
        [SerializeField, Tooltip("ノード生成範囲")] private BoxCollider _generationArea;
        [SerializeField, Tooltip("生成範囲未設定時の代替サイズ")] private Vector3 _fallbackAreaSize = new(50f, 10f, 50f);
        [SerializeField, Tooltip("ノード間の目安距離")] private float _poissonRadius = 5f;
        [SerializeField, Tooltip("候補生成回数")] private int _poissonCandidateCount = 30;
        [SerializeField, Tooltip("最大ノード数。0なら無制限")] private int _maxNodeCount = 0;
        [SerializeField, Tooltip("ワープ先や孤立した足場などの追加シード地点")] private Transform[] _isolatedSeedPoints;
        [SerializeField, Tooltip("初期ランダムシード点の探索回数（孤立した島を拾いやすくするための試行数）")] private int _initialNavMeshSeedCount = 10;

        [Header("Connect")]
        [SerializeField, Tooltip("接続可能距離")] private float _connectDistance = 10f;
        [SerializeField, Tooltip("1ノードあたりの最大接続数")] private int _maxConnectCount = 4;

        [Header("Raycast")]
        [SerializeField, Tooltip("Raycast 距離")] private float _rayDistance = 10f;
        [SerializeField, Tooltip("開始地点の上方向補正")] private float _rayUpAmount = 1f;
        [SerializeField, Tooltip("地面判定時の上方向補正")] private float _groundRayUp = 2f;
        [SerializeField, Tooltip("障害物判定用レイヤー")] private LayerMask _obstacleMask = ~0;

        [Header("Precision")]
        [SerializeField, Tooltip("座標量子化精度")] private float _positionQuantize = 1000f;

        [Header("NavMesh")]
        [SerializeField, Tooltip("サンプリング許容距離")] private float _navMeshTolerance = 2f;

        [Header("Vault")]
        [SerializeField] private CapsuleCollider _botCapsuleCollider;
        [SerializeField, Tooltip("VaultNode接続の最大許容探索距離")] private float _vaultMaxConnectDistance = 3.0f;
        [SerializeField] private float _maxLedgeHeight = 1.5f;
        [SerializeField] private float _minLedgeHeight = 0.4f;
        [SerializeField] private float _maxLedgeDepth = 0.9f;
        [SerializeField] private float _reachDistance = 2f;
        [SerializeField] private float _timeToVault = 0.5f;
        [SerializeField] private AnimationCurve _vaultCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float _groundSlopeThreshold = 45f;
        [SerializeField] private LayerMask _groundLayer = ~0;

        [Header("Filter")]
        [SerializeField, Tooltip("Originへ到達できない孤立ノードを自動排除するか")] private bool _filterUnreachableNodes = true;

        private NavMeshPath _path;
        private Vector3 _offset;
        private float _invCell;
        private bool _isGenerating;

        public List<NodeData>[,,] Nodes;

        private static readonly Vector3[] directions =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
        };

        private static readonly Vector3Int[] neighborOffsets = BuildNeighborOffsets();

        private static Vector3Int[] BuildNeighborOffsets()
        {
            var list = new List<Vector3Int>(26);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;
                        list.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            return list.ToArray();
        }
        /// <summary>
        /// 開始時に自分自身と飛び越えノードを削除する
        /// </summary>
        public void Awake()
        {
            Destroy(this);

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }

        [Button("ノード生成")]
        public void Generate()
        {
#if UNITY_EDITOR
            GenerateAsync().Forget(Debug.LogException);
#endif
        }

        public async UniTask GenerateAsync()
        {
            if (_isGenerating) return;
            _isGenerating = true;

            try
            {
                ValidateSettings();

                if (_requirePathToOrigin && _origin == null)
                    throw new InvalidOperationException("Require Path To Origin が有効ですが、Origin が設定されていません。");

                if (_origin == null && _generationArea == null)
                    throw new InvalidOperationException("Origin または Generation Area のいずれかを設定してください。");

                if (_botCapsuleCollider == null)
                    throw new InvalidOperationException("Bot Capsule Collider が設定されていません。");

                Nodes = null;

#if UNITY_EDITOR
                _mapData?.ClearList();
#endif

                Debug.Log("=== Generate Start ===");

                var bounds = GetGenerationBounds();
                var sampledPositions = GeneratePoissonSamples(bounds);

                if (sampledPositions.Count == 0)
                {
                    Debug.LogWarning("生成対象ノードがありません。");
                    return;
                }

                var nodeList = new List<NodeData>(sampledPositions.Count);
                var nodeCache = new Dictionary<Vector3Int, NodeData>(sampledPositions.Count);

                int index = 0;
                for (int i = 0; i < sampledPositions.Count; i++)
                {
                    CreateNodeData(sampledPositions[i], ref index, nodeCache, nodeList);
                }

                if (nodeList.Count == 0)
                {
                    Debug.LogWarning("NodeData 生成後にノードが0件になりました。");
                    return;
                }

                // グリッド化
                BuildNodeGrid(nodeList);

                // 飛び越え接続を作る
                var vaultNodes = GetComponentsInChildren<VaultNode>();
                int vaultConnectedCount = 0;

                foreach (var vaultNode in vaultNodes)
                {
                    if (vaultNode == null) continue;

                    var startNode = GetNearestNode(vaultNode.GetStartPos(), _vaultMaxConnectDistance);
                    var endNode = GetNearestNode(vaultNode.GetEndPos(), _vaultMaxConnectDistance);

                    if (startNode == null || endNode == null)
                    {
                        Debug.LogWarning($"[VaultNode] 接続ノードが見つかりませんでした (距離上限 {_vaultMaxConnectDistance}m 内)。Start: {vaultNode.GetStartPos()} -> {(startNode != null ? "OK" : "None")}, End: {vaultNode.GetEndPos()} -> {(endNode != null ? "OK" : "None")}", vaultNode);
                        continue;
                    }

                    if (startNode == endNode)
                    {
                        Debug.LogWarning($"[VaultNode] 開始ノードと終了ノードが同一です。NodeIndex: {startNode.NodeIndex}", vaultNode);
                        continue;
                    }

                    startNode.SetVaultConnect(endNode);
                    vaultConnectedCount++;
                    Debug.Log($"[VaultNode] 接続設定完了: Node[{startNode.NodeIndex}] -> Node[{endNode.NodeIndex}]");
                }

                // 接続を作る
                await BuildConnectionsAsync();

                // 到達不能ノードの排除
                if (_filterUnreachableNodes)
                {
                    var originPos = _origin != null ? _origin.position : (_generationArea != null ? _generationArea.bounds.center : transform.position);
                    nodeList = FilterUnreachableNodes(nodeList, originPos);
                    BuildNodeGrid(nodeList);
                }

#if UNITY_EDITOR
                foreach (var node in nodeList)
                {
                    _mapData?.AddNodeData(node);
                }

                if (_mapData != null)
                {
                    EditorUtility.SetDirty(_mapData);
                    AssetDatabase.SaveAssets();
                }

                // シーン内の NodeProvider のキャッシュを更新
                foreach (var provider in FindObjectsOfType<NodeProvider>())
                {
                    provider.Reload();
                }
#endif

                Debug.Log("=== Generate Complete ===");
            }
            finally
            {
                _isGenerating = false;
            }
        }

        /// <summary>
        /// Origin へ到達・合流可能なノードのみを抽出し、不要な孤立ノードを排除する
        /// </summary>
        private List<NodeData> FilterUnreachableNodes(List<NodeData> nodes, Vector3 originPos)
        {
            if (nodes == null || nodes.Count == 0) return nodes;

            // 1. Origin の最寄りノードを取得（距離制限なしで必ず一番近いノードを取得）
            var originNode = GetNearestNode(originPos, float.MaxValue);
            if (originNode == null)
            {
                Debug.LogWarning("[NodeGenerator] FilterUnreachableNodes: 最寄りノードが見つからなかったため、ノード排除をスキップします。");
                return nodes;
            }

            Debug.Log($"[NodeGenerator] 到達性判定の起点ノード: Node[{originNode.NodeIndex}] (位置: {originNode.Position})");

            // 2. 逆向きエッジマップ（逆走グラフ）を構築
            // 通常接続は双方向、Vault 接続は start -> end なので逆走は end -> start
            var reverseGraph = new Dictionary<NodeData, List<NodeData>>(nodes.Count);
            foreach (var node in nodes)
            {
                reverseGraph[node] = new List<NodeData>();
            }

            foreach (var node in nodes)
            {
                // 通常接続（双方向）
                if (node.ConnectNode != null)
                {
                    foreach (var neighbor in node.ConnectNode)
                    {
                        if (neighbor != null && reverseGraph.ContainsKey(neighbor))
                        {
                            reverseGraph[neighbor].Add(node);
                        }
                    }
                }

                // Vault 接続（node -> node.VaultConnect のため、VaultConnect から node への逆走エッジを追加）
                if (node.VaultConnect != null && reverseGraph.ContainsKey(node.VaultConnect))
                {
                    reverseGraph[node.VaultConnect].Add(node);
                }
            }

            // 3. Origin ノードから逆方向 BFS 探索
            var reachableSet = new HashSet<NodeData>();
            var queue = new Queue<NodeData>();

            reachableSet.Add(originNode);
            queue.Enqueue(originNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (reverseGraph.TryGetValue(current, out var predecessors))
                {
                    foreach (var pred in predecessors)
                    {
                        if (reachableSet.Add(pred))
                        {
                            queue.Enqueue(pred);
                        }
                    }
                }
            }

            // 4. 不要ノードの除去と接続のクリーンアップ
            var filteredList = new List<NodeData>(reachableSet.Count);
            int newIndex = 0;

            foreach (var node in nodes)
            {
                if (!reachableSet.Contains(node)) continue;

                // 到達不能ノードへの通常接続を削除
                if (node.ConnectNode != null)
                {
                    node.ConnectNode.RemoveAll(c => c == null || !reachableSet.Contains(c));
                }

                // 到達不能ノードへの Vault 接続を解除
                if (node.VaultConnect != null && !reachableSet.Contains(node.VaultConnect))
                {
                    node.SetVaultConnect(null);
                }

                node.SetIndex(newIndex++);
                filteredList.Add(node);
            }

            int removedCount = nodes.Count - filteredList.Count;
            Debug.Log($"[NodeGenerator] 到達不能ノードを排除しました: 元 {nodes.Count} 件 -> 有効 {filteredList.Count} 件 (削除: {removedCount} 件)");

            return filteredList;
        }

        /// <summary>
        /// ノード生成範囲を取得
        /// </summary>
        private Bounds GetGenerationBounds()
        {
            if (_generationArea != null)
            {
                return _generationArea.bounds;
            }

            var center = _origin != null ? _origin.position : transform.position;
            return new Bounds(center, _fallbackAreaSize);
        }

        /// <summary>
        /// Poisson Disk Sampling によりノード候補を生成
        /// </summary>
        private List<Vector3> GeneratePoissonSamples(Bounds bounds)
        {
            var samples = new List<Vector3>();
            var activeList = new List<Vector3>();

            // 1. 指定された孤立シード地点（ワープ地点や空中足場など）からシードを追加
            if (_isolatedSeedPoints != null)
            {
                foreach (var seedTransform in _isolatedSeedPoints)
                {
                    if (seedTransform == null) continue;
                    if (TryProjectToNavMesh(seedTransform.position, out var navPoint) && bounds.Contains(navPoint))
                    {
                        navPoint = GetGroundPosition(navPoint, 10);
                        if (IsFarEnough(navPoint, samples, _poissonRadius))
                        {
                            samples.Add(navPoint);
                            activeList.Add(navPoint);
                        }
                    }
                }
            }

            // 2. Bounds内のランダムなNavMesh地点から初期シードを探索
            int seedSearchCount = Mathf.Max(1, _initialNavMeshSeedCount);
            for (int i = 0; i < seedSearchCount; i++)
            {
                if (TryGetRandomNavMeshPoint(bounds, out var seedPoint))
                {
                    seedPoint = GetGroundPosition(seedPoint, 10);
                    if (IsFarEnough(seedPoint, samples, _poissonRadius))
                    {
                        samples.Add(seedPoint);
                        activeList.Add(seedPoint);
                    }
                }
            }

            if (samples.Count == 0)
            {
                Debug.LogWarning("NavMesh 上に初期点を見つけられませんでした。");
                return samples;
            }

            // 3. 各シードからポアソンディスクサンプリングを拡散
            while (activeList.Count > 0)
            {
                if (_maxNodeCount > 0 && samples.Count >= _maxNodeCount)
                    break;

                int activeIndex = UnityEngine.Random.Range(0, activeList.Count);
                var origin = activeList[activeIndex];
                bool found = false;

                for (int i = 0; i < _poissonCandidateCount; i++)
                {
                    var candidate = GenerateCandidate(origin, _poissonRadius);

                    if (!bounds.Contains(candidate))
                        continue;

                    if (!TryProjectToNavMesh(candidate, out var hitPoint))
                        continue;

                    if (!bounds.Contains(hitPoint))
                        continue;

                    if (!IsFarEnough(hitPoint, samples, _poissonRadius))
                        continue;

                    samples.Add(hitPoint);
                    activeList.Add(hitPoint);
                    found = true;
                    break;
                }

                if (!found)
                {
                    activeList.RemoveAt(activeIndex);
                }
            }

            return samples;
        }

        private Vector3 GetGroundPosition(Vector3 position, float distance)
        {
            Ray ray = new(position, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, distance))
            {
                return hit.point;
            }

            return position;
        }
        /// <summary>
        /// 候補位置を生成
        /// </summary>
        private Vector3 GenerateCandidate(Vector3 origin, float radius)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float distance = UnityEngine.Random.Range(radius, radius * 2f);

            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            return origin + offset;
        }

        /// <summary>
        /// ランダムな NavMesh 上の地点を取得
        /// </summary>
        private bool TryGetRandomNavMeshPoint(Bounds bounds, out Vector3 result)
        {
            const int maxTry = 128;

            for (int i = 0; i < maxTry; i++)
            {
                var randomPoint = new Vector3(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
                );

                if (!TryProjectToNavMesh(randomPoint, out result))
                    continue;

                if (!bounds.Contains(result))
                    continue;

                if (_requirePathToOrigin && _origin != null && !HasValidPath(result, _origin.position))
                    continue;

                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// 指定座標を NavMesh 上へ補正
        /// </summary>
        private bool TryProjectToNavMesh(Vector3 position, out Vector3 result)
        {
            if (NavMesh.SamplePosition(position, out var hit, _navMeshTolerance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// 周囲ノードとの最低距離を満たしているか
        /// </summary>
        private bool IsFarEnough(Vector3 candidate, List<Vector3> samples, float minDistance)
        {
            float minDistanceSq = minDistance * minDistance;

            for (int i = 0; i < samples.Count; i++)
            {
                if ((candidate - samples[i]).sqrMagnitude < minDistanceSq)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// NodeData 作成
        /// </summary>
        private bool CreateNodeData(
            Vector3 position,
            ref int index,
            Dictionary<Vector3Int, NodeData> cache,
            List<NodeData> output,
            out NodeData node)
        {
            node = null;

            if (_requirePathToOrigin && _origin != null && !HasValidPath(position, _origin.position))
                return false;

            var key = Quantize(position);
            if (cache.TryGetValue(key, out node))
                return true;

            node = new NodeData(position, index++);
            cache.Add(key, node);
            output.Add(node);

            return true;
        }

        /// <summary>
        /// NodeData 作成簡易版
        /// </summary>
        private NodeData CreateNodeData(
            Vector3 position,
            ref int index,
            Dictionary<Vector3Int, NodeData> cache,
            List<NodeData> output)
        {
            if (!CreateNodeData(position, ref index, cache, output, out var node))
                return null;

            return node;
        }

        /// <summary>
        /// 座標量子化
        /// </summary>
        private Vector3Int Quantize(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x * _positionQuantize),
                Mathf.RoundToInt(position.y * _positionQuantize),
                Mathf.RoundToInt(position.z * _positionQuantize)
            );
        }

        /// <summary>
        /// ノードを空間グリッドへ格納
        /// </summary>
        private void BuildNodeGrid(List<NodeData> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                Nodes = null;
                return;
            }

            Vector3 min = nodes[0].Position;
            Vector3 max = nodes[0].Position;

            for (int i = 1; i < nodes.Count; i++)
            {
                min = Vector3.Min(min, nodes[i].Position);
                max = Vector3.Max(max, nodes[i].Position);
            }

            Vector3 size = max - min;

            Vector3Int gridSize = new(
                Mathf.Max(1, Mathf.CeilToInt(size.x / _connectDistance) + 1),
                Mathf.Max(1, Mathf.CeilToInt(size.y / _connectDistance) + 1),
                Mathf.Max(1, Mathf.CeilToInt(size.z / _connectDistance) + 1)
            );

            Nodes = new List<NodeData>[gridSize.x, gridSize.y, gridSize.z];

            _invCell = 1f / _connectDistance;
            _offset = -min;
            _offset += Vector3.one * 0.0001f;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var idx = WorldToIndex(node.Position);

                Nodes[idx.x, idx.y, idx.z] ??= new List<NodeData>(2);
                Nodes[idx.x, idx.y, idx.z].Add(node);
            }
        }

        /// <summary>
        /// ノード接続生成
        /// </summary>
        private async UniTask BuildConnectionsAsync()
        {
            if (Nodes == null) return;

            float maxDistSq = _connectDistance * _connectDistance;

            int sx = Nodes.GetLength(0);
            int sy = Nodes.GetLength(1);
            int sz = Nodes.GetLength(2);

            for (int x = 0; x < sx; x++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int z = 0; z < sz; z++)
                    {
                        var cell = Nodes[x, y, z];
                        if (cell == null) continue;

                        foreach (var node in cell)
                        {
                            int currentCount = node.ConnectNode?.Count ?? 0;
                            if (currentCount >= _maxConnectCount) continue;

                            var candidates = new List<(NodeData other, float distSq)>(32);

                            for (int ox = -1; ox <= 1; ox++)
                            {
                                for (int oy = -1; oy <= 1; oy++)
                                {
                                    for (int oz = -1; oz <= 1; oz++)
                                    {
                                        int nx = x + ox;
                                        int ny = y + oy;
                                        int nz = z + oz;

                                        if (nx < 0 || ny < 0 || nz < 0 ||
                                            nx >= sx || ny >= sy || nz >= sz)
                                            continue;

                                        var neighbor = Nodes[nx, ny, nz];
                                        if (neighbor == null) continue;

                                        foreach (var other in neighbor)
                                        {
                                            if (ReferenceEquals(node, other)) continue;
                                            if (node.ConnectNode != null && node.ConnectNode.Contains(other)) continue;

                                            float distSq = (node.Position - other.Position).sqrMagnitude;
                                            if (distSq > maxDistSq) continue;

                                            candidates.Add((other, distSq));
                                        }
                                    }
                                }
                            }

                            candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                            foreach (var (other, distSq) in candidates)
                            {
                                if ((node.ConnectNode?.Count ?? 0) >= _maxConnectCount) break;
                                if (other == null) continue;
                                if ((other.ConnectNode?.Count ?? 0) >= _maxConnectCount) continue;
                                if (node.ConnectNode != null && node.ConnectNode.Contains(other)) continue;

                                if (!AStarSystem.ConnectivityCheck(node.Position, other.Position, true)) continue;

                                if (HasObstacle(node.Position, other.Position)) continue;

                                node.AddConnect(other);
                                other.AddConnect(node);
                            }
                        }
                    }
                }

                if ((x & 1) == 0)
                    await UniTask.Yield();
            }
        }

        /// <summary>
        /// パラメータ検証
        /// </summary>
        private void ValidateSettings()
        {
            if (_poissonRadius <= 0f)
                throw new InvalidOperationException("Poisson Radius は 0 より大きくしてください。");

            if (_poissonCandidateCount <= 0)
                throw new InvalidOperationException("Poisson Candidate Count は 0 より大きくしてください。");

            if (_removeInvalid(_connectDistance))
                throw new InvalidOperationException("Connect Distance は 0 より大きくしてください。");

            if (_positionQuantize <= 0f)
                throw new InvalidOperationException("Position Quantize は 0 より大きくしてください。");

            if (_navMeshTolerance <= 0f)
                throw new InvalidOperationException("NavMesh Tolerance は 0 より大きくしてください。");
        }

        /// <summary>
        /// 無効値判定
        /// </summary>
        private bool _removeInvalid(float value) => value <= 0f;

        /// <summary>
        /// 障害物有無判定
        /// </summary>
        private bool HasObstacle(Vector3 from, Vector3 to)
        {
            from += Vector3.up * _rayUpAmount;
            to += Vector3.up * _rayUpAmount;

            Vector3 dir = to - from;
            float distance = dir.magnitude;

            if (distance <= 0.0001f) return false;

            return Physics.Raycast(
                from,
                dir / distance,
                distance,
                _obstacleMask,
                QueryTriggerInteraction.Ignore
            );
        }

        /// <summary>
        /// NavMesh 上で経路到達可能か
        /// </summary>
        private bool HasValidPath(Vector3 from, Vector3 to)
        {
            _path ??= new NavMeshPath();

            if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path))
            {
                return _path.status == NavMeshPathStatus.PathComplete;
            }

            return false;
        }

        /// <summary>
        /// ワールド座標をグリッドインデックスへ変換
        /// </summary>
        private Vector3Int WorldToIndex(Vector3 pos)
        {
            int ix = Mathf.FloorToInt((pos.x + _offset.x) * _invCell);
            int iy = Mathf.FloorToInt((pos.y + _offset.y) * _invCell);
            int iz = Mathf.FloorToInt((pos.z + _offset.z) * _invCell);

            ix = Mathf.Clamp(ix, 0, Nodes.GetLength(0) - 1);
            iy = Mathf.Clamp(iy, 0, Nodes.GetLength(1) - 1);
            iz = Mathf.Clamp(iz, 0, Nodes.GetLength(2) - 1);

            return new Vector3Int(ix, iy, iz);
        }

        private void OnDrawGizmos()
        {
            if (!_isDrawGizumoIcon || Nodes == null) return;

            var offset = Vector3.up * 0.3f;
            var cam = Camera.current;
            if (cam == null) return;

            var camPos = cam.transform.position;

            int sx = Nodes.GetLength(0);
            int sy = Nodes.GetLength(1);
            int sz = Nodes.GetLength(2);

            for (int x = 0; x < sx; x++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int z = 0; z < sz; z++)
                    {
                        var cell = Nodes[x, y, z];
                        if (cell == null) continue;

                        for (int i = 0; i < cell.Count; i++)
                        {
                            var node = cell[i];
                            if (node == null) continue;

                            if ((node.Position - camPos).sqrMagnitude > 400f) continue;

                            var pos = node.Position + offset;

                            var connects = node.ConnectNode;
                            if (connects != null)
                            {
                                for (int j = 0; j < connects.Count; j++)
                                {
                                    var connect = connects[j];
                                    if (connect == null) continue;

                                    if (node.GetHashCode() > connect.GetHashCode()) continue;

                                    Gizmos.color = Color.white;
                                    Gizmos.DrawLine(pos, connect.Position + offset);
                                }
                            }

                            if (node.VaultConnect != null)
                            {
                                Gizmos.color = Color.green;
                                Gizmos.DrawLine(pos, node.VaultConnect.Position + offset);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 指定座標に最も近いノード取得（maxDistance を超える場合は null）
        /// </summary>
        private NodeData GetNearestNode(Vector3 pos, float maxDistance = float.MaxValue)
        {
            float minDistance = maxDistance;
            NodeData nodeData = null;
            if (Nodes == null) return null;

            foreach (var cell in Nodes)
            {
                if (cell == null) continue;
                foreach (var node in cell)
                {
                    float distance = Vector3.Distance(pos, node.Position);

                    if (distance < minDistance)
                    {
                        nodeData = node;
                        minDistance = distance;
                    }
                }
            }

            return nodeData;
        }

        /// <summary>
        /// ノード一覧 Gizmos 描画
        /// </summary>
        public static void DrawGizmos(List<NodeData> nodes)
        {
            var offset = Vector3.up * 0.3f;
            var cam = Camera.current;

            if (cam == null) return;
            var camPos = cam.transform.position;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;

                // カメラから遠いノードはスキップ
                if ((node.Position - camPos).sqrMagnitude > 400f) continue;

                var pos = node.Position + offset;

                var connects = node.ConnectNode;
                if (connects != null)
                {
                    for (int j = 0; j < connects.Count; j++)
                    {
                        var connect = connects[j];
                        if (connect == null) continue;

                        // 重複描画防止
                        if (node.GetHashCode() > connect.GetHashCode()) continue;
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(pos, connect.Position + offset);
                    }
                }

                if (node.VaultConnect != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(pos, node.VaultConnect.Position + offset);
                }
            }
        }
    }
}