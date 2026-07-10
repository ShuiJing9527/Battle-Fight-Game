using System.Collections;
using UnityEngine;

public class Player01RThrustVfx : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Transform thrustSpriteAnimationRoot;
    [SerializeField] private ScissorFrameEffectPlayer sequencePlayer;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;

    [Header("Frames")]
    [SerializeField] private Sprite[] spriteFrames;
    [SerializeField, Min(1f)] private float frameRate = 14f;
    [SerializeField] private bool playOnEnable = false;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool autoHideOnComplete = true;

    [Header("Placement")]
    [SerializeField] private Vector3 localPositionOffset = new Vector3(1.1f, 0.35f, 0f);
    [SerializeField] private Vector3 localRotation;
    [SerializeField] private Vector3 localScale = Vector3.one;
    [SerializeField] private bool mirrorPositionOffsetWithFacing = true;
    [SerializeField] private bool sourceFramesFaceLeft = true;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int orderInLayer = 90;

    [Header("Playback")]
    [SerializeField, Min(0.01f)] private float fallbackLifetime = 0.2f;
    [SerializeField] private bool destroyRootOnComplete = true;

    private Coroutine autoCleanupRoutine;

    private void Awake()
    {
        ResolveBindings();
        ApplyStaticSettings();
        if (!playOnEnable)
        {
            SetVisualVisible(false);
        }
    }

    private void OnEnable()
    {
        ResolveBindings();
        ApplyStaticSettings();
        if (playOnEnable)
        {
            Play(1f);
        }
        else
        {
            SetVisualVisible(false);
        }
    }

    private void OnDisable()
    {
        if (autoCleanupRoutine != null)
        {
            StopCoroutine(autoCleanupRoutine);
            autoCleanupRoutine = null;
        }
    }

    public void Play(float facingSign)
    {
        ResolveBindings();
        ApplyStaticSettings();
        ApplyPlacementAndFacing(facingSign);

        bool hasFrames = spriteFrames != null && spriteFrames.Length > 0;
        if (!hasFrames || sequencePlayer == null || targetSpriteRenderer == null)
        {
            SetVisualVisible(false);
            if (destroyRootOnComplete)
            {
                Destroy(gameObject);
            }

            return;
        }

        targetSpriteRenderer.enabled = true;
        targetSpriteRenderer.flipX = facingSign < 0f;
        sequencePlayer.SetFrames(spriteFrames);
        sequencePlayer.SetFrameRate(frameRate);
        sequencePlayer.SetPlayOnEnable(false);
        sequencePlayer.SetLoop(loop);
        sequencePlayer.SetAutoHideOnComplete(autoHideOnComplete);
        sequencePlayer.SetScaleRange(localScale, localScale);
        sequencePlayer.SetLifetime(ResolvePlaybackLifetime());
        sequencePlayer.SetDestroyOnComplete(false);
        sequencePlayer.SetSorting(sortingLayerName, orderInLayer);
        sequencePlayer.Play();
        ApplyPlacementAndFacing(facingSign);

        if (autoCleanupRoutine != null)
        {
            StopCoroutine(autoCleanupRoutine);
        }

        autoCleanupRoutine = StartCoroutine(AutoCleanupRoutine(ResolvePlaybackLifetime(), loop));
    }

    public void SetSpriteFrames(Sprite[] frames)
    {
        spriteFrames = frames;
        if (sequencePlayer != null)
        {
            sequencePlayer.SetFrames(spriteFrames);
        }
    }

    private void ResolveBindings()
    {
        if (sequencePlayer == null)
        {
            sequencePlayer = GetComponentInChildren<ScissorFrameEffectPlayer>(true);
        }

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (thrustSpriteAnimationRoot == null)
        {
            if (sequencePlayer != null)
            {
                thrustSpriteAnimationRoot = sequencePlayer.transform;
            }
            else
            {
                thrustSpriteAnimationRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }
        }
    }

    private void ApplyStaticSettings()
    {
        if (sequencePlayer == null)
        {
            return;
        }

        sequencePlayer.SetPlayOnEnable(false);
        sequencePlayer.SetLoop(loop);
        sequencePlayer.SetAutoHideOnComplete(autoHideOnComplete);
        sequencePlayer.SetDestroyOnComplete(false);
        sequencePlayer.SetLifetime(ResolvePlaybackLifetime());
        sequencePlayer.SetFrameRate(frameRate);
        sequencePlayer.SetScaleRange(localScale, localScale);
        sequencePlayer.SetFrames(spriteFrames);
        sequencePlayer.SetSorting(sortingLayerName, orderInLayer);
    }

    private void ApplyPlacementAndFacing(float facingSign)
    {
        ResolveBindings();
        bool facingRight = facingSign > 0f;
        bool shouldFlipX = sourceFramesFaceLeft ? facingRight : !facingRight;
        Vector3 resolvedLocalPosition = localPositionOffset;

        if (thrustSpriteAnimationRoot != null)
        {
            if (mirrorPositionOffsetWithFacing)
            {
                resolvedLocalPosition.x = facingRight
                    ? Mathf.Abs(localPositionOffset.x)
                    : -Mathf.Abs(localPositionOffset.x);
            }

            thrustSpriteAnimationRoot.localPosition = resolvedLocalPosition;
            thrustSpriteAnimationRoot.localRotation = Quaternion.Euler(localRotation);
            thrustSpriteAnimationRoot.localScale = localScale;
        }

        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.flipX = shouldFlipX;
        }

        Debug.Log(
            $"[Player01 R ThrustVfx] facingSign={facingSign:F2}, facing={(facingRight ? "Right" : "Left")}, " +
            $"sourceFramesFaceLeft={sourceFramesFaceLeft}, flipX={shouldFlipX}, localPosition={resolvedLocalPosition}",
            this);
    }

    private float ResolvePlaybackLifetime()
    {
        if (spriteFrames != null && spriteFrames.Length > 0 && frameRate > 0f)
        {
            return Mathf.Max(0.01f, spriteFrames.Length / frameRate);
        }

        return Mathf.Max(0.01f, fallbackLifetime);
    }

    private IEnumerator AutoCleanupRoutine(float playbackDuration, bool isLooping)
    {
        if (isLooping)
        {
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, playbackDuration) + 0.05f);
        SetVisualVisible(false);
        autoCleanupRoutine = null;

        if (destroyRootOnComplete)
        {
            Destroy(gameObject);
        }
    }

    private void SetVisualVisible(bool visible)
    {
        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.enabled = visible && spriteFrames != null && spriteFrames.Length > 0;
            if (!targetSpriteRenderer.enabled)
            {
                targetSpriteRenderer.sprite = null;
            }
        }
    }
}
