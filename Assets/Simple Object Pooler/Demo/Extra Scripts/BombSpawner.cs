using UnityEngine;
using JoaoMilone.Demo;
using JoaoMilone.Pooler.Controller;

public class BombSpawner : MonoBehaviour
{
    [SerializeField]
    private GameObject bombPrefab;

    [SerializeField]
    private float bombRate = 0.75f;

    [SerializeField]
    private RandomPositionInArea spawnArea;

    private void Start()
    {
        InvokeRepeating(nameof(BombSpawn), 0, bombRate);
    }

    private void BombSpawn()
    {
        ObjectPooler.ME.RequestObject(bombPrefab.name, spawnArea.SpawnPosition());
    }

    private void OnDisable()
    {
        CancelInvoke();
    }
}
