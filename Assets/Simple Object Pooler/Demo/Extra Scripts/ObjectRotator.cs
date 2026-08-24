using UnityEngine;

namespace JoaoMilone.Demo
{
    public class ObjectRotator : MonoBehaviour
    {
        [SerializeField] private bool rotateX = false, rotateY = false, rotateZ = true;
        [SerializeField] private float vel_Rotation = 300f;

        private Transform targetTransform;

        private void Awake() => targetTransform = transform;

        private void Update()
        {
            RotateObject();
        }

        private void RotateObject()
        {
            if (rotateX)
                RotateTransform(targetTransform, new Vector3(vel_Rotation * Time.deltaTime, 0, 0));

            if (rotateY)
                RotateTransform(targetTransform, new Vector3(0, vel_Rotation * Time.deltaTime, 0));

            if (rotateZ)
                RotateTransform(targetTransform, new Vector3(0, 0, vel_Rotation * Time.deltaTime));
        }

        private static void RotateTransform(Transform target, Vector3 rotationToSet) =>
            target.Rotate(rotationToSet, Space.Self);
    }
}
