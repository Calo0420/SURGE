using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class VFXFrameAnimator : MonoBehaviour
{
    [Header("Sequence Data")]
    [SerializeField] private List<Sprite> animationFrames = new();
    [SerializeField] private float framesPerSecond = 30f;

    [Header("Rendering")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Coroutine playRoutine;
    private SimpleVFXPool ownerPool;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void InitializePool(SimpleVFXPool pool)
    {
        ownerPool = pool;
    }

    public void Play(Color tintColor, float hdrIntensity = 2f)
    {
        if (animationFrames.Count == 0)
        {
            Debug.LogError($"{nameof(VFXFrameAnimator)} on '{name}' has no animation frames assigned.", this);
            ReturnToPoolOrDisable();
            return;
        }

        if (framesPerSecond <= 0f)
        {
            Debug.LogError($"{nameof(VFXFrameAnimator)} on '{name}' must use a positive FPS value.", this);
            ReturnToPoolOrDisable();
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        spriteRenderer.color = tintColor * hdrIntensity;
        playRoutine = StartCoroutine(AnimateSequence());
    }

    private IEnumerator AnimateSequence()
    {
        float frameDuration = 1f / framesPerSecond;
        WaitForSeconds wait = new(frameDuration);

        for (int i = 0; i < animationFrames.Count; i++)
        {
            spriteRenderer.sprite = animationFrames[i];
            yield return wait;
        }

        spriteRenderer.sprite = null;
        playRoutine = null;
        ReturnToPoolOrDisable();
    }

    private void OnDisable()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
        }
    }

    private void ReturnToPoolOrDisable()
    {
        if (ownerPool != null)
        {
            ownerPool.ReturnToPool(this);
            return;
        }

        gameObject.SetActive(false);
    }
}
