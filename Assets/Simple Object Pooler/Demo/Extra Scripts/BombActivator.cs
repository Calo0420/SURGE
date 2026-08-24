using UnityEngine;
using JoaoMilone.Pooler.Controller;

public class BombActivator : MonoBehaviour
{
    [SerializeField]
    private string idToSpawn = "bomb_FX";

    private const string TargetTag = "Finish";
    
    private void Explode()
    {
        ObjectPooler.ME.RequestObject(idToSpawn, transform);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(TargetTag)) return;
        Explode();
        gameObject.SetActive(false);
    }
}

