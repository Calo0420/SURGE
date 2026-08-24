using UnityEngine;

namespace JoaoMilone.Demo
{
    public class SimplePlayerControl : MonoBehaviour
    {
        [SerializeField] private float velocity = 15;
        [SerializeField] private Transform checkPoint;
        
        private void FixedUpdate()
        {
            PlayerControl();
            AimControl();
        }

        private void Update()
        {
            CheckIfOutOfBounds();
        }

        private void CheckIfOutOfBounds() 
        {
            if (transform.position.y < -7)
                transform.position = checkPoint.position;
        }

        private void AimControl() 
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);

            if (!plane.Raycast(ray, out var distance)) return;
            
            var target = ray.GetPoint(distance);
            var direction = target - transform.position;
            var rotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
        
        private void PlayerControl() 
        {
            Vector3 playerDirection;

            if (Application.platform == RuntimePlatform.Android)
            {
                var x = -Input.acceleration.y;
                var z = Input.acceleration.x;

                playerDirection = new Vector3(x, 0, z);
            }
            else
            {
                var x = Input.GetAxisRaw("Horizontal");
                var z = Input.GetAxisRaw("Vertical");

                playerDirection = new Vector3(x, 0, z);
            }
            
            transform.Translate(playerDirection.normalized * velocity * Time.fixedDeltaTime, Space.World);
        }
    }
}
