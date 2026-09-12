using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace September.InGame.Tutorial
{
    /// <summary>現在の移動目標と、プレイヤーから目標までの歩行経路を表示する。</summary>
    public class TutorialMoveGuide : MonoBehaviour
    {
        [SerializeField] private GameObject _targetPrefab;
        [SerializeField] private GameObject _guidePrefab;
        [SerializeField] private bool _realtime = true;
        [SerializeField, Min(0.05f)] private float _updateInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float _movementThreshold = 0.25f;
        [SerializeField, Min(0.05f)] private float _width = 0.5f;
        [SerializeField, Min(0.1f)] private float _segmentLength = 0.75f;
        [SerializeField, Min(0.1f)] private float _navMeshSearchDistance = 2.5f;
        [SerializeField] private float _heightOffset = 0.15f;

        private Transform _player;
        private Transform _target;
        private GameObject _marker;
        private GameObject _guide;
        private ParticleSystem[] _particles;
        private Mesh _mesh;
        private NavMeshPath _path;
        private readonly Vector3[] _corners = new Vector3[64];
        private readonly List<Vector3> _vertices = new(4096);
        private readonly List<int> _triangles = new(6144);
        private Vector3 _lastPlayer;
        private Vector3 _lastTarget;
        private float _nextUpdate;
        private bool _hasPath;

        private void Awake()
        {
            // Unity内部の経路データは、コンポーネントの生成が完了してから作成する。
            _path = new NavMeshPath();
        }

        public void Show(Transform player, Transform target)
        {
            Hide();
            if (!player || !target || !_targetPrefab || !_guidePrefab)
            {
                Debug.LogError("移動ガイドのプレイヤー・目的地・VFXを設定してください。", this);
                return;
            }
            _player = player;
            _target = target;
            if (!_marker)
            {
                _marker = Instantiate(_targetPrefab, transform);
                _guide = Instantiate(_guidePrefab, transform);
                _guide.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _guide.transform.localScale = Vector3.one;
                _mesh = new Mesh { name = "Tutorial route emission mesh" };
                _mesh.MarkDynamic();
                _particles = _guide.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var particle in _particles)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    // 発生位置の座標系を揃え、素材の色・テクスチャ・粒子表現を経路上で使用する。
                    particle.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    particle.transform.localScale = Vector3.one;
                    var main = particle.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var shape = particle.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Mesh;
                    shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                    shape.position = Vector3.zero;
                    shape.rotation = Vector3.zero;
                    shape.scale = Vector3.one;
                    shape.useMeshColors = false;
                }
            }
            _marker.SetActive(true);
            foreach (var particle in _marker.GetComponentsInChildren<ParticleSystem>(true))
                particle.Play();
            UpdateRoute();
        }

        private void Update()
        {
            if (!_target || !_player)
            {
                if (_marker && _marker.activeSelf) Hide();
                return;
            }
            if (!_realtime || Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + _updateInterval;
            // 静止中は再計算しない。経路未取得時はNavMeshの準備完了を待って再試行する。
            float threshold = _movementThreshold * _movementThreshold;
            if (_hasPath && (_player.position - _lastPlayer).sqrMagnitude < threshold &&
                (_target.position - _lastTarget).sqrMagnitude < threshold) return;
            UpdateRoute();
        }

        private void UpdateRoute()
        {
            _lastPlayer = _player.position;
            _lastTarget = _target.position;
            _nextUpdate = Time.unscaledTime + _updateInterval;
            _marker.transform.position = _target.position + Vector3.up * _heightOffset;
            _hasPath = false;
            if (!NavMesh.SamplePosition(_player.position, out var start, _navMeshSearchDistance, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(_target.position, out var end, _navMeshSearchDistance, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) ||
                _path.status != NavMeshPathStatus.PathComplete)
            {
                // 壁や海を横切る直線では誘導しない。経路がないときも目的地自体は表示する。
                _guide.SetActive(false);
                return;
            }
            _marker.transform.position = end.position + Vector3.up * _heightOffset;
            int count = _path.GetCornersNonAlloc(_corners);
            if (count < 2 || count >= _corners.Length)
            {
                _guide.SetActive(false);
                return;
            }
            _vertices.Clear();
            _triangles.Clear();
            for (int i = 1; i < count; i++)
            {
                Vector3 a = _corners[i - 1];
                Vector3 b = _corners[i];
                Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized * (_width * 0.5f);
                if (side.sqrMagnitude < 0.0001f) continue;
                // 長い区間も分割するが、頂点数が際限なく増えないよう上限を設ける。
                int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / _segmentLength), 1, 32);
                for (int j = 0; j < steps; j++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)j / steps) + Vector3.up * _heightOffset;
                    Vector3 q = Vector3.Lerp(a, b, (float)(j + 1) / steps) + Vector3.up * _heightOffset;
                    int index = _vertices.Count;
                    _vertices.Add(p - side); _vertices.Add(p + side);
                    _vertices.Add(q - side); _vertices.Add(q + side);
                    _triangles.Add(index); _triangles.Add(index + 2); _triangles.Add(index + 1);
                    _triangles.Add(index + 1); _triangles.Add(index + 2); _triangles.Add(index + 3);
                }
            }
            if (_vertices.Count == 0) { _guide.SetActive(false); return; }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            // 同じMeshとVFXを再利用し、目標変更時以外は粒子を継続表示する。
            _guide.SetActive(true);
            foreach (var particle in _particles)
            {
                var shape = particle.shape;
                shape.mesh = _mesh;
                if (!particle.isPlaying) particle.Play();
            }
            _hasPath = true;
        }

        public void Hide()
        {
            _player = null;
            _target = null;
            _hasPath = false;
            if (_marker)
            {
                // 次の目的地へ移したとき、前の地点の粒子を持ち越さない。
                foreach (var particle in _marker.GetComponentsInChildren<ParticleSystem>(true))
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _marker.SetActive(false);
            }
            if (_guide)
            {
                foreach (var particle in _particles)
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _guide.SetActive(false);
            }
        }

        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
        }
    }
}
