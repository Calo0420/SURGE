using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class VFXSparkStreakController : MonoBehaviour
{
    [SerializeField] private ParticleSystem sparkParticleSystem;

    private void Awake()
    {
        if (sparkParticleSystem == null)
        {
            sparkParticleSystem = GetComponent<ParticleSystem>();
        }
    }

    public void EmitSparks(Vector3 position, Vector2 direction, Color targetColor, int count = 12)
    {
        if (sparkParticleSystem == null)
        {
            Debug.LogError($"{nameof(VFXSparkStreakController)} on '{name}' has no particle system.", this);
            return;
        }

        if (count <= 0)
        {
            Debug.LogError($"{nameof(VFXSparkStreakController)} received a non-positive count.", this);
            return;
        }

        Vector2 safeDirection = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
        transform.position = position;

        float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var main = sparkParticleSystem.main;
        main.startColor = new ParticleSystem.MinMaxGradient(targetColor);
        sparkParticleSystem.Emit(count);
    }
}
