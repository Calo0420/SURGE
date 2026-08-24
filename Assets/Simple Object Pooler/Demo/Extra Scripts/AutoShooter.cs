using JoaoMilone.Pooler.Controller;
using UnityEngine;

namespace JoaoMilone.Demo
{
    public class AutoShooter : MonoBehaviour
    {
        [SerializeField]
        private string idBullet;

        [SerializeField]
        private Transform tfBulletExitPoint;

        [SerializeField]
        private float fireRate = 0.5f;

        private float auxTimer;

        private void Awake() => auxTimer = fireRate;

        private void Update() => ShootingRoutine();

        private void ShootingRoutine() 
        {
            auxTimer -= Time.deltaTime;

            if (auxTimer <= 0)
                Shoot();
        }

        private void Shoot() 
        {
            ObjectPooler.ME.RequestObject(idBullet, tfBulletExitPoint);
            auxTimer = fireRate;
        }
    }
}
