using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ScissorFrameEffectPlayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.01f)] private float lifetime = 0.16f;
    [SerializeField, Min(1f)] private float frameRate = 24f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop;
    [SerializeField] private bool autoHideOnComplete = true;
    [SerializeField] private bool destroyOnComplete = true;
    [SerializeField] private bool fadeOut = true;
    [SerializeField] private Vector3 startScale = Vector3.one;
    [SerializeField] private Vector3 endScale = Vector3.one;

    private float elapsed;
    private bool playing;
    private Color baseColor = Color.white;
    private float scaleDirectionX = 1f;

    private void Awake()
    {
        ResolveRenderer();
        CacheBaseColor();
    }

    private void OnEnable()
    {
        ApplyRendererVisibility(false);
        if (playOnEnable)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        float safeLifetime = Mathf.Max(0.01f, lifetime);
        elapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(elapsed / safeLifetime);

        ApplyFrame(normalized);
        ApplyScale(normalized);
        ApplyAlpha(normalized);

        if (elapsed >= safeLifetime)
        {
            CompletePlayback();
        }
    }

    public void Play()
    {
        ResolveRenderer();
        CacheBaseColor();
        elapsed = 0f;
        scaleDirectionX = transform.localScale.x < 0f ? -1f : 1f;
        if (targetRenderer == null || frames == null || frames.Length == 0)
        {
            playing = false;
            ApplyRendererVisibility(false);
            return;
        }

        playing = true;
        ApplyRendererVisibility(true);
        ApplyScale(0f);
        ApplyFrame(0f);
        ApplyAlpha(0f);
    }

    public void SetLifetime(float value)
    {
        lifetime = Mathf.Max(0.01f, value);
    }

    public void SetFrames(Sprite[] value)
    {
        frames = value;
        ResolveRenderer();
        if (targetRenderer != null)
        {
            targetRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        }
    }

    public void SetFrameRate(float value)
    {
        frameRate = Mathf.Max(1f, value);
    }

    public void SetPlayOnEnable(bool value)
    {
        playOnEnable = value;
    }

    public void SetLoop(bool value)
    {
        loop = value;
    }

    public void SetAutoHideOnComplete(bool value)
    {
        autoHideOnComplete = value;
    }

    public void SetSortingOrder(int order)
    {
        ResolveRenderer();
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = order;
        }
    }

    public void SetSorting(string sortingLayerName, int order)
    {
        ResolveRenderer();
        if (targetRenderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
        {
            targetRenderer.sortingLayerName = sortingLayerName;
        }

        targetRenderer.sortingOrder = order;
    }

    public void SetColor(Color color)
    {
        ResolveRenderer();
        baseColor = color;
        if (targetRenderer != null)
        {
            targetRenderer.color = color;
        }
    }

    public void SetDestroyOnComplete(bool value)
    {
        destroyOnComplete = value;
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void CacheBaseColor()
    {
        if (targetRenderer != null)
        {
            baseColor = targetRenderer.color;
        }
    }

    private void ApplyFrame(float normalized)
    {
        if (targetRenderer == null || frames == null || frames.Length == 0)
        {
            ApplyRendererVisibility(false);
            return;
        }

        ApplyRendererVisibility(true);
        int lastIndex = Mathf.Max(0, frames.Length - 1);
        int index;
        if (loop)
        {
            index = Mathf.FloorToInt(elapsed * Mathf.Max(1f, frameRate));
            index = frames.Length > 0 ? index % frames.Length : 0;
        }
        else
        {
            index = Mathf.Clamp(Mathf.FloorToInt(elapsed * Mathf.Max(1f, frameRate)), 0, lastIndex);
        }

        targetRenderer.sprite = frames[index];
    }

    private void ApplyScale(float normalized)
    {
        Vector3 scale = Vector3.LerpUnclamped(startScale, endScale, normalized);
        scale.x = Mathf.Abs(scale.x) * scaleDirectionX;
        transform.localScale = scale;
    }

    private void ApplyAlpha(float normalized)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Color color = baseColor;
        if (fadeOut)
        {
            color.a = Mathf.Lerp(baseColor.a, 0f, normalized);
        }

        targetRenderer.color = color;
    }

    private void CompletePlayback()
    {
        playing = false;
        ApplyFrame(1f);
        ApplyScale(1f);
        ApplyAlpha(1f);

        if (loop)
        {
            elapsed = 0f;
            playing = true;
            return;
        }

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
        else if (autoHideOnComplete)
        {
            ApplyRendererVisibility(false);
            gameObject.SetActive(false);
        }
    }

    private void ApplyRendererVisibility(bool visible)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.enabled = visible;
        if (!visible)
        {
            targetRenderer.sprite = null;
        }
    }
}
