using System;
using UnityEngine;

namespace September.InGame.Fields
{
    public class OutOfFieldArea : SingletonMonoBehaviour<OutOfFieldArea>
    {
        [SerializeField] private float _outOfFieldHeight;

        public bool IsOutOfField(Vector3 position) => position.y <= _outOfFieldHeight;

        private void OnDrawGizmosSelected()
        {
            const int size = 100;

            Gizmos.color = new Color(1, 0, 0, 0.5f);

            Gizmos.DrawCube(Vector3.up * _outOfFieldHeight, new Vector3(size, 0, size));
        }
    }
}
