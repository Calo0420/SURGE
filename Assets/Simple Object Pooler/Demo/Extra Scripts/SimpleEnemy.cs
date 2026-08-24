using JoaoMilone.Pooler.Controller;
using UnityEngine;

namespace JoaoMilone.Demo 
{
    public class SimpleEnemy : MonoBehaviour
    {
        [SerializeField] private Rigidbody rb;
        [SerializeField] private string idEnemie_Explosion;
        [SerializeField] private float vel_enemy = 5;
        [SerializeField] private int EnemyHealth = 4;

        private Transform playerTransform;

        //Please don't get transform target like this in a real game xD lol
        private void Awake() => playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        
        private void OnDisable() => rb.linearVelocity = Vector3.zero;
        
        private void Update() => FollowPlayer();
        
        private void FollowPlayer()
        {
            var position = transform.position;
            var direction = (playerTransform.position - position).normalized;

            rb.MovePosition(position + direction * Time.deltaTime * vel_enemy);
        }

        private void TakeDamage(int qtyDamage) 
        {
            EnemyHealth -= qtyDamage;

            if (EnemyHealth <= 0)
                KillEnemy();
        }

        private void KillEnemy()
        {
            ObjectPooler.ME.RequestObject(idEnemie_Explosion, transform.position);
            gameObject.SetActive(false);
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player"))
                KillEnemy();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Respawn"))
                TakeDamage(1);
        }
    }
}
