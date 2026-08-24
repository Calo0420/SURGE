using UnityEngine;

namespace JoaoMilone.Demo
{
    public class BulletForce : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody rb;

        [SerializeField]
        private float bulletForce = 150;

        private void OnEnable()
        {
            //As your using ObjectPooling for this objects you might want to use your basic start functions on OnEnable()
            AddForce();
        }

        private void OnDisable()
        {
            //And as your deactivating and reusing, you might want to reset things such as velocity 
            rb.linearVelocity = Vector3.zero;
        }

        private void AddForce() 
        {
            rb.AddForce(transform.forward * bulletForce);
        }
    }
}
