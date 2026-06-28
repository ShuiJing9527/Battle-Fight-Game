using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ScissorFrameEffectPlayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.01f)] private float lifetime = 0.16f;
    [SerializeField] private bool playOnEnable = true;
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
        playing = true;
        scaleDirectionX = transform.localScale.x < 0f ? -1f : 1f;
        ApplyScale(0f);
        ApplyFrame(0f);
        ApplyAlpha(0f);
    }

    public void SetLifetime(float value)
    {
        lifetime = Mathf.Max(0.01f, value);
    }

    public void SetSortingOrder(int order)
    {
        ResolveRenderer();
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = order;
        }
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
            return;
        }

        int lastIndex = Mathf.Max(0, frames.Length - 1);
        int index = Mathf.Clamp(Mathf.FloorToInt(normalized * frames.Length), 0, lastIndex);
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

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
