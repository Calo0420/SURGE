using JoaoMilone.Pooler.Controller;
using UnityEngine;

namespace JoaoMilone.Demo
{
    public class BulletParticleSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject particleToSpawn;

        private const string TargetTag = "Finish";
        
        private void SpawnParticle() 
        {
            ObjectPooler.ME.RequestObject(particleToSpawn.name, transform.position, Quaternion.identity);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag(TargetTag)) return;
            
            SpawnParticle();
            gameObject.SetActive(false);
        }
    }
}
