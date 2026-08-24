using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JoaoMilone.Demo
{
    public class RandomPositionInArea : MonoBehaviour
    {
        private float xRange = 15f, zRange = 10f;

        [SerializeField]
        private Color GizmosColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmosColor;
            var transform1 = transform;
            Gizmos.DrawCube(transform1.position, transform1.localScale);
        }

        private void Start()
        {
            var localScale = transform.localScale;
            xRange = localScale.x / 2f;
            zRange = localScale.z / 2f;
        }

        private static float Range(float x, float range) => x + Random.Range(-range, range);

        public Vector3 SpawnPosition()
        {
            var position = transform.position;
            return new Vector3(Range(position.x, xRange), position.y, Range(position.z, zRange));
        }
    }
}
