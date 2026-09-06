using System.Collections.Generic;
using UnityEngine;
namespace InGame.Bot
{
    public class NodeData
    {
        public Vector3 Position { get; private set; }
        public NodeState State { get; private set; }
        public int NodeIndex { get; private set; }
        public List<NodeData> ConnectNode { get; private set; }
        public NodeData Parent { get; private set; }
        public NodeData VaultConnect { get; private set; }
        public bool IsValut;
        public float Cost => GetCost();

        private float GetCost()
        {
            return StartDistance + _goalDistance - ConnectNode.Count;
        }

        public float StartDistance { get; private set; }
        private float _goalDistance;
        public NodeData(Vector3 positioin, int index)
        {
            Position = positioin;
            ConnectNode = new();
            NodeIndex = index;
            ResetState();
        }

        public NodeData(Vector3 position, int index, List<NodeData> connectNode)
        {
            Debug.Log(connectNode.Count);
            Position = position;
            ConnectNode = new List<NodeData>(connectNode);
            NodeIndex = index;
            ResetState();
        }

        /// <summary>
        /// 接続ノードを登録する
        /// </summary>
        /// <param name="data">接続するノード</param>
        public void AddConnect(NodeData data)
        {
            if (ConnectNode.Contains(data))
            {
                Debug.Log("接続ノードが重複しています");
                return;
            }

            ConnectNode.Add(data);
        }

        public void ResetState()
        {
            State = NodeState.None;
            StartDistance = 0;
            _goalDistance = 0;
            IsValut = false;
        }

        /// <summary>
        /// コストを計算する
        /// </summary>
        /// <returns></returns>
        public float GetAllCost()
        {
            if (State == NodeState.None)
            {
                Debug.LogError("コストが入力されていません");
                return 0;
            }

            return StartDistance + _goalDistance;
        }

        /// <summary>
        /// ノードをOpenにする
        /// </summary>
        /// <param name="startDis">開始地点からのみちのり</param>
        /// <param name="goalDis">終了地点からの直線距離</param>
        public void OpenNode(float startDis, float goalDis)
        {
            StartDistance = startDis;
            _goalDistance = goalDis;

            State = NodeState.Open;
        }

        public void Close()
        {
            State = NodeState.Closed;
        }

        public void SetParent(NodeData parent)
        {
            Parent = parent;
        }

        public void SetVaultConnect(NodeData nodeData)
        {
            VaultConnect = nodeData;
        }

        public void SetIndex(int index)
        {
            NodeIndex = index;
        }
    }

    public enum NodeState
    {
        None, Open, Closed
    }
}