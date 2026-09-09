using UnityEngine;
using UnityEngine.Splines;

namespace September
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ZiplineCableRendererDisplay : MonoBehaviour
    {
        [SerializeField] private SplineContainer _splineContainer;
        [SerializeField, Tooltip("スプライン方向の分割数(多いほど滑らか)")]
        private int _lengthSegments = 100;
        [SerializeField, Range(3, 32), Tooltip("円周方向の分割数(多いほど丸くなる)")]
        private int _radialSegments = 12;
        [SerializeField, Tooltip("チューブの半径")]
        private float _radius = 0.15f;

        private MeshFilter _meshFilter;
        private Mesh _mesh;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            GenerateMesh();

            if (_splineContainer != null && _splineContainer.Spline != null)
            {
                Spline.Changed += OnSplineChanged;
            }
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (_splineContainer == null || spline != _splineContainer.Spline) return;
            GenerateMesh();
        }

        /// <summary>
        /// チューブのメッシュを生成する
        /// </summary>
        [ContextMenu("Generate Tube")]
        private void GenerateMesh()
        {
            if (_splineContainer == null) return;
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            
            if (_mesh == null) _mesh = new Mesh { name = "SplineTube" };
            _mesh.Clear();

            // メッシュの頂点数と三角形数を計算
            int vertCountPerRing = _radialSegments + 1;
            var vertices = new Vector3[(_lengthSegments + 1) * vertCountPerRing];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[_lengthSegments * _radialSegments * 6];

            Vector3 prevRight = Vector3.right;
            // スプライン上の各点でチューブの頂点を計算
            for (int i = 0; i <= _lengthSegments; i++)
            {
                // スプライン上のパラメータtを計算
                float t = (float)i / _lengthSegments;
                _splineContainer.Evaluate(t, out var position, out var tangentF, out var upF);
                // Evaluateが返す座標はワールド座標相当のため、このオブジェクトのローカル座標に変換する
                Vector3 localPosition = transform.InverseTransformPoint(position);

                // 接線と上方向を正規化
                Vector3 forward = ((Vector3)tangentF).normalized;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

                Vector3 normalUp = ((Vector3)upF).normalized;
                // 上方向が接線とほぼ平行な場合は、上方向を補正
                if (normalUp.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(normalUp, forward)) > 0.99f)
                {
                    normalUp = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
                }

                // 接線と上方向から右方向を計算
                Vector3 right = Vector3.Cross(normalUp, forward).normalized;
                // 右方向の向きを前のフレームと比較して反転させることで、チューブがねじれないようにする
                if (Vector3.Dot(right, prevRight) < 0f)
                {
                    right = -right;
                }
                prevRight = right;
                // 接線と右方向から最終的な上方向を計算
                Vector3 finalUp = Vector3.Cross(forward, right).normalized;
                // 円周方向の頂点を計算
                for (int j = 0; j <= _radialSegments; j++)
                {
                    float angle = (float)j / _radialSegments * Mathf.PI * 2f;
                    Vector3 circleDir = right * Mathf.Cos(angle) + finalUp * Mathf.Sin(angle);

                    int index = i * vertCountPerRing + j;
                    vertices[index] = localPosition + circleDir * _radius;  // ← ここを修正
                    normals[index] = circleDir;
                    uvs[index] = new Vector2((float)j / _radialSegments, t);
                }
            }
            // 三角形インデックスを計算
            int triIndex = 0;
            for (int i = 0; i < _lengthSegments; i++)
            {
                // 各セグメントの三角形を計算
                for (int j = 0; j < _radialSegments; j++)
                {
                    int current = i * vertCountPerRing + j;
                    int next = current + vertCountPerRing;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next + 1;
                    triangles[triIndex++] = next;
                }
            }
            // メッシュに頂点、法線、UV、三角形を設定
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            //エディタ上でスプラインが変更されたときにメッシュを更新するために、遅延コールを使用
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                GenerateMesh();
                
            };
        }
#endif
    }
}
