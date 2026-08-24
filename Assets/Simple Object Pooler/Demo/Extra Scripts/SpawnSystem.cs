using JoaoMilone.Pooler.Controller;
using UnityEngine;

namespace JoaoMilone.Demo
{
    public class SpawnSystem : MonoBehaviour
    {
        [SerializeField]
        private int qtdMaxEnemies = 10;

        [SerializeField]
        private string idEnemie = "enemy";

        private RandomPositionInArea[] spawnPositions;

        private void Awake() => GetSpawns();
        
        private void GetSpawns() => spawnPositions = FindObjectsOfType<RandomPositionInArea>();
        
        private void LateUpdate()
        {
            if (FindObjectsOfType<SimpleEnemy>().Length < qtdMaxEnemies)
                SpawnEnemy();
        }

        private void SpawnEnemy() 
        {
            var spawnPosition = spawnPositions[Random.Range(0, spawnPositions.Length)].SpawnPosition();
            ObjectPooler.ME.RequestObject(idEnemie, spawnPosition);
        }
    }
}
