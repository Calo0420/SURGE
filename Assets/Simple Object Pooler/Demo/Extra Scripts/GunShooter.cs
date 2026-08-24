using JoaoMilone.Pooler.Controller;
using UnityEngine;

namespace JoaoMilone.Demo 
{
    public class GunShooter : MonoBehaviour
    {
        [SerializeField]
        private string idNormalBullet, idShotGunBullet;

        [SerializeField]
        private Transform bulletExitPoint;

        [SerializeField]
        private Transform[] shotgunExitPoint;

        [SerializeField]
        private float fireRate = 0.2f;

        private bool isShooting = false;
        private float auxTimer = 0;

        private ObjectPooler Pooler => ObjectPooler.ME;

        private void Awake() => auxTimer = 0;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
                isShooting = true;

            if (Input.GetKeyUp(KeyCode.Mouse0))
                isShooting = false;

            if (Input.GetKeyDown(KeyCode.Mouse1))
                ShootShotgun();

            if (Input.GetKeyDown(KeyCode.C))
                ObjectPooler.ME.ClearPoolWithID(idShotGunBullet);

            if (Input.GetKeyDown(KeyCode.Z))
                ObjectPooler.ME.ClearEntirePool();

            ShootNormal();
        }

        private void ShootNormal()
        {
            if (!isShooting) return;
            
            auxTimer -= Time.deltaTime;

            if (!(auxTimer <= 0)) return;
            Pooler.RequestObject(idNormalBullet, bulletExitPoint);
            auxTimer = fireRate;
        }

        private void ShootShotgun()
        {
            foreach(var tf in shotgunExitPoint)
                Pooler.RequestObject(idShotGunBullet, tf);
        }
    }
}
