using UnityEngine;

namespace JoaoMilone.Demo
{
    public class SimpleCameraFollow : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private float smoothTime = 0.125f, maxSpeed = 50;

        [SerializeField]
        private Vector3 offset;

        private Vector3 camVelo;

        private void FixedUpdate()
        {
            if (!target.gameObject.activeSelf)
                return;

            var desiredPosition = target.position + offset;
            var smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref camVelo, smoothTime, maxSpeed, Time.fixedDeltaTime);
            transform.position = smoothedPosition;
        }
    }
}
