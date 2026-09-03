using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float targetScreenRadiusPixels = 80f;

    [Header("Occluder Filter")]
    [SerializeField] private string generatedPropsRootName = "PropsRoot";
    [SerializeField] private bool requireGeneratedPropsRoot = false;
    [SerializeField, Min(0.1f)] private float minimumRendererHeight = 2.5f;
    [SerializeField, Min(0.05f)] private float rescanInterval = 0.5f;

    [Header("Fade")]
    [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.28f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 9f;
    [SerializeField, Min(0f)] private float screenPaddingPixels = 24f;
    [SerializeField] private bool useFallbackTransparentMaterial = true;

    private readonly List<SpriteRenderer> candidates = new List<SpriteRenderer>();
    private readonly HashSet<SpriteRenderer> occludingThisFrame = new HashSet<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new Dictionary<SpriteRenderer, Material>();
    private Camera cachedCamera;
    private Material fallbackTransparentMaterial;
    private float nextRescanTime;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
        if (fallbackTransparentMaterial != null)
        {
            Destroy(fallbackTransparentMaterial);
            fallbackTransparentMaterial = null;
        }
    }

    private void LateUpdate()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        if (cachedCamera == null || target == null)
        {
            RestoreAll();
            return;
        }

        if (Time.unscaledTime >= nextRescanTime)
        {
            RebuildCandidates();
            nextRescanTime = Time.unscaledTime + rescanInterval;
        }

        Vector3 targetWorld = target.position + targetOffset;
        Vector3 targetViewport = cachedCamera.WorldToViewportPoint(targetWorld);
        if (targetViewport.z <= 0f)
        {
            RestoreAll();
            return;
        }

        occludingThisFrame.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            SpriteRenderer spriteRenderer = candidates[i];
            if (IsOccludingTarget(spriteRenderer, targetViewport))
            {
                occludingThisFrame.Add(spriteRenderer);
                FadeToward(spriteRenderer, fadedAlpha);
            }
        }

        RestoreNonOccluding();
    }

    private void RebuildCandidates()
    {
        candidates.Clear();

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            if (!spriteRenderer.enabled)
            {
                continue;
            }

            if (spriteRenderer.bounds.size.y < minimumRendererHeight)
            {
                continue;
            }

            if (IsTargetRenderer(spriteRenderer.transform))
            {
                continue;
            }

            if (spriteRenderer.GetComponentInParent<Collider>() == null)
            {
                continue;
            }

            if (requireGeneratedPropsRoot && !IsUnderGeneratedPropsRoot(spriteRenderer.transform))
            {
                continue;
            }

            candidates.Add(spriteRenderer);
            if (!originalColors.ContainsKey(spriteRenderer))
            {
                originalColors.Add(spriteRenderer, spriteRenderer.color);
            }

            if (!originalMaterials.ContainsKey(spriteRenderer))
            {
                originalMaterials.Add(spriteRenderer, spriteRenderer.sharedMaterial);
            }
        }
    }

    private bool IsOccludingTarget(SpriteRenderer spriteRenderer, Vector3 targetViewport)
    {
        if (spriteRenderer == null || !spriteRenderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!spriteRenderer.enabled)
        {
            return false;
        }

        Bounds bounds = spriteRenderer.bounds;
        Vector3 centerViewport = cachedCamera.WorldToViewportPoint(bounds.center);
        if (centerViewport.z <= 0f || centerViewport.z >= targetViewport.z)
        {
            return false;
        }

        Rect viewportRect = CalculateViewportRect(bounds);
        float paddingX = screenPaddingPixels / Mathf.Max(1, cachedCamera.pixelWidth);
        float paddingY = screenPaddingPixels / Mathf.Max(1, cachedCamera.pixelHeight);
        viewportRect.xMin -= paddingX;
        viewportRect.xMax += paddingX;
        viewportRect.yMin -= paddingY;
        viewportRect.yMax += paddingY;

        float targetRadiusX = targetScreenRadiusPixels / Mathf.Max(1, cachedCamera.pixelWidth);
        float targetRadiusY = targetScreenRadiusPixels / Mathf.Max(1, cachedCamera.pixelHeight);
        Rect targetRect = Rect.MinMaxRect(
            targetViewport.x - targetRadiusX,
            targetViewport.y - targetRadiusY,
            targetViewport.x + targetRadiusX,
            targetViewport.y + targetRadiusY);

        return viewportRect.Overlaps(targetRect);
    }

    private Rect CalculateViewportRect(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 viewportPoint = cachedCamera.WorldToViewportPoint(corners[i]);
            minX = Mathf.Min(minX, viewportPoint.x);
            minY = Mathf.Min(minY, viewportPoint.y);
            maxX = Mathf.Max(maxX, viewportPoint.x);
            maxY = Mathf.Max(maxY, viewportPoint.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private bool IsUnderGeneratedPropsRoot(Transform current)
    {
        while (current != null)
        {
            if (current.name == generatedPropsRootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsTargetRenderer(Transform current)
    {
        if (target == null)
        {
            return false;
        }

        while (current != null)
        {
            if (current == target)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void FadeToward(SpriteRenderer spriteRenderer, float targetAlpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (useFallbackTransparentMaterial && !SupportsAlpha(spriteRenderer))
        {
            ApplyFallbackTransparentMaterial(spriteRenderer);
        }

        Color color = spriteRenderer.color;
        color.a = Mathf.MoveTowards(color.a, targetAlpha, fadeSpeed * Time.deltaTime);
        spriteRenderer.color = color;
    }

    private void RestoreNonOccluding()
    {
        List<SpriteRenderer> keys = new List<SpriteRenderer>(originalColors.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            SpriteRenderer spriteRenderer = keys[i];
            if (spriteRenderer == null)
            {
                originalColors.Remove(spriteRenderer);
                continue;
            }

            if (occludingThisFrame.Contains(spriteRenderer))
            {
                continue;
            }

            Color original = originalColors[spriteRenderer];
            Color color = spriteRenderer.color;
            color.a = Mathf.MoveTowards(color.a, original.a, fadeSpeed * Time.deltaTime);
            spriteRenderer.color = color;

            if (Mathf.Approximately(color.a, original.a))
            {
                RestoreOriginalMaterial(spriteRenderer);
            }
        }
    }

    private void RestoreAll()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> entry in originalColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = entry.Value;
                RestoreOriginalMaterial(entry.Key);
            }
        }

        occludingThisFrame.Clear();
    }

    private static bool SupportsAlpha(SpriteRenderer spriteRenderer)
    {
        Material material = spriteRenderer != null ? spriteRenderer.sharedMaterial : null;
        if (material == null)
        {
            return true;
        }

        return material.HasProperty("_Color")
            || material.HasProperty("_BaseColor")
            || material.HasProperty("_TintColor");
    }

    private void ApplyFallbackTransparentMaterial(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (!originalMaterials.ContainsKey(spriteRenderer))
        {
            originalMaterials.Add(spriteRenderer, spriteRenderer.sharedMaterial);
        }

        Material material = GetFallbackTransparentMaterial();
        if (material != null && spriteRenderer.sharedMaterial != material)
        {
            spriteRenderer.sharedMaterial = material;
        }
    }

    private void RestoreOriginalMaterial(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (originalMaterials.TryGetValue(spriteRenderer, out Material originalMaterial) &&
            originalMaterial != null &&
            spriteRenderer.sharedMaterial != originalMaterial)
        {
            spriteRenderer.sharedMaterial = originalMaterial;
        }
    }

    private Material GetFallbackTransparentMaterial()
    {
        if (fallbackTransparentMaterial != null)
        {
            return fallbackTransparentMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        if (shader == null)
        {
            return null;
        }

        fallbackTransparentMaterial = new Material(shader)
        {
            name = "Runtime Camera Occlusion Sprite Fade"
        };
        fallbackTransparentMaterial.hideFlags = HideFlags.HideAndDontSave;
        return fallbackTransparentMaterial;
    }
}
