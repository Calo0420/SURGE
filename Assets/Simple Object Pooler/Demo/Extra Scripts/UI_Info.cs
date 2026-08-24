using JoaoMilone.Pooler.Controller;
using UnityEngine;
using UnityEngine.UI;

namespace JoaoMilone.Demo
{
    public class UI_Info : MonoBehaviour
    {
        [SerializeField] private Text txtTotalAmount;
        [SerializeField] private Text txtTotalActive;
        [SerializeField] private Text txtTotalYellowBullets;
        [SerializeField] private Text txtTotalActiveYellowBullets;

        private void UpdateUI()
        {
            if (txtTotalAmount != null) txtTotalAmount.text = "Total Objects In Pool: " + ObjectPooler.ME.TotalObjectsInPool();
            if (txtTotalActive != null) txtTotalActive.text = "Total Active Objects: " + ObjectPooler.ME.TotalActiveObjects();
            if (txtTotalYellowBullets != null) txtTotalYellowBullets.text = "Total Yellow Bullets: " + ObjectPooler.ME.CountObjectWithID("bullet_yellow");
            if (txtTotalActiveYellowBullets != null) txtTotalActiveYellowBullets.text = "Total Active Yellow Bullets: " + ObjectPooler.ME.CountActivatedObjectWithID("bullet_yellow");
        }

        // Update is called once per frame
        private void Update()
        {
            UpdateUI();
        }
    }
}
