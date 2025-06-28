using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.Combat
{
    [Serializable]
    public class HitboxAction
    {
        public string name;
        public List<Transform> points = new();
        public int startFrame;
        public int endFrame;
        [NonSerialized] public bool IsActive;
    
        // 🔽 追加：このアクションで既にヒットしたコライダー
        [NonSerialized] public HashSet<Collider> AlreadyHitColliders = new();

        public void ResetHitCache()
        {
            AlreadyHitColliders.Clear();
        }
    }

    [Serializable]
    public class HitboxTimeline : MonoBehaviour
    {
        [SerializeField] private List<HitboxAction> actions = new();
        [NonSerialized] public HitboxManager Manager;

        private void Awake()
        {
            Manager = FindAnyObjectByType<HitboxManager>();
            if (Manager != null)
            {
                Manager.RegisterTimeline(this);
            }
        }

        public void EvaluateAction(string actionName, int currentFrame)
        {
            var action = actions.Find(a => a.name == actionName);
            if (action == null) return;

            bool inRange = currentFrame >= action.startFrame && currentFrame <= action.endFrame;

            // 開始した瞬間だけリセット
            if (!action.IsActive && inRange)
            {
                action.ResetHitCache();
            }

            action.IsActive = inRange;

            if (inRange)
            {
                Manager?.ProcessHitboxAction(action);
            }
        }

        public List<HitboxAction> GetAllActions() => actions;
    }
} 
