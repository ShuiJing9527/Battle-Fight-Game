using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RadianceMarkStatus : MonoBehaviour
{
    private const string DefaultMarkVisualPrefabAssetPath = "Assets/Prefabs/UI/StatusMarks/RadianceMarkIcon.prefab";
    private const string DefaultMarkIconSpriteAssetPath = "Assets/Prefabs/UI/DayNightAffinity/Textures/Icon_Radiance_Sun.png";

    [SerializeField, Min(0f)] private float remainingDuration;
    [Header("Visual")]
    [SerializeField] private GameObject markVisualPrefab;
    [SerializeField] private Sprite markIconSprite;
    [SerializeField] private Vector3 markWorldOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 markVisualScale = new Vector3(0.6f, 0.6f, 0.6f);
    [SerializeField, Min(0f)] private float markHeightPadding = 0.2f;
    [SerializeField] private int markSortingOrder = 100;
    [SerializeField] private Color markTint = Color.white;
    [SerializeField] private bool faceCamera = true;

    private GameObject spawnedMarkVisual;
    private SpriteRenderer spawnedMarkRenderer;
    private Camera cachedCamera;
    private bool consumeRequested;

    public bool IsActive => remainingDuration > 0f;
    public float RemainingDuration => Mathf.Max(0f, remainingDuration);

    public void ConfigureVisual(GameObject visualPrefab, Sprite iconSprite)
    {
        if (visualPrefab != null)
        {
            markVisualPrefab = visualPrefab;
        }

        if (iconSprite != null)
        {
            markIconSprite = iconSprite;
        }

        ApplyVisualAppearance();
    }

    public void ApplyOrRefresh(float duration)
    {
        EnsureVisualDefaultsAssigned();
        remainingDuration = Mathf.Max(remainingDuration, Mathf.Max(0f, duration));
        enabled = remainingDuration > 0f;
        if (remainingDuration > 0f)
        {
            EnsureVisualInstance();
            UpdateVisualTransform();
        }
    }

    public static void ApplyOrRefresh(GameObject target, float duration, GameObject visualPrefab = null, Sprite iconSprite = null)
    {
        if (target == null)
        {
            return;
        }

        RadianceMarkStatus status = target.GetComponent<RadianceMarkStatus>();
        if (status == null)
        {
            status = target.AddComponent<RadianceMarkStatus>();
        }

        status.ConfigureVisual(visualPrefab, iconSprite);
        status.ApplyOrRefresh(duration);
    }

    public bool Consume()
    {
        if (consumeRequested || remainingDuration <= 0f)
        {
            return false;
        }

        consumeRequested = true;
        remainingDuration = 0f;
        enabled = false;
        DestroyVisual();
        Destroy(this);
        return true;
    }

    private void Update()
    {
        if (remainingDuration <= 0f)
        {
            Destroy(this);
            return;
        }

        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (remainingDuration <= 0f)
        {
            Destroy(this);
        }
    }

    private void LateUpdate()
    {
        if (remainingDuration > 0f)
        {
            EnsureVisualInstance();
            UpdateVisualTransform();
        }
    }

    private void OnDestroy()
    {
        DestroyVisual();
    }

    private void EnsureVisualDefaultsAssigned()
    {
#if UNITY_EDITOR
        if (markVisualPrefab == null)
        {
            markVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMarkVisualPrefabAssetPath);
        }

        if (markIconSprite == null)
        {
            markIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultMarkIconSpriteAssetPath);
        }
#endif
    }

    private void EnsureVisualInstance()
    {
        if (spawnedMarkVisual != null)
        {
            ApplyVisualAppearance();
            return;
        }

        GameObject visual = markVisualPrefab != null
            ? Instantiate(markVisualPrefab)
            : new GameObject($"{name}_RadianceMarkIcon");

        visual.name = "RadianceMarkIconInstance";
        spawnedMarkVisual = visual;
        spawnedMarkRenderer = visual.GetComponentInChildren<SpriteRenderer>(true);
        if (spawnedMarkRenderer == null)
        {
            spawnedMarkRenderer = visual.AddComponent<SpriteRenderer>();
        }

        ApplyVisualAppearance();
        UpdateVisualTransform();
    }

    private void ApplyVisualAppearance()
    {
        if (spawnedMarkVisual == null)
        {
            return;
        }

        if (spawnedMarkRenderer == null)
        {
            spawnedMarkRenderer = spawnedMarkVisual.GetComponentInChildren<SpriteRenderer>(true);
        }

        spawnedMarkVisual.transform.localScale = markVisualScale;

        if (spawnedMarkRenderer == null)
        {
            return;
        }

        if (markIconSprite != null)
        {
            spawnedMarkRenderer.sprite = markIconSprite;
        }

        Color appliedColor = markTint;
        appliedColor.a = 1f;
        spawnedMarkRenderer.color = appliedColor;
        spawnedMarkRenderer.sortingOrder = markSortingOrder;
    }

    private void UpdateVisualTransform()
    {
        if (spawnedMarkVisual == null)
        {
            return;
        }

        Vector3 anchorPosition = ResolveMarkAnchorPosition(out string anchorSource);
        Vector3 finalWorldPosition = anchorPosition + markWorldOffset;
        spawnedMarkVisual.transform.position = finalWorldPosition;

        if (!faceCamera)
        {
            return;
        }

        Camera targetCamera = ResolveTargetCamera();
        if (targetCamera == null)
        {
            return;
        }

        spawnedMarkVisual.transform.rotation = Quaternion.LookRotation(targetCamera.transform.forward, targetCamera.transform.up);
    }

    private Vector3 ResolveMarkAnchorPosition(out string anchorSource)
    {
        bool hasBounds = false;
        Bounds combinedBounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (spawnedMarkRenderer != null && renderer == spawnedMarkRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            anchorSource = "Renderer";
            return new Vector3(combinedBounds.center.x, combinedBounds.max.y + markHeightPadding, combinedBounds.center.z);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            anchorSource = "Fallback";
            Vector3 fallbackPosition = transform.position + Vector3.up * 1.5f;
            return fallbackPosition;
        }

        anchorSource = "Collider";
        return new Vector3(combinedBounds.center.x, combinedBounds.max.y + markHeightPadding, combinedBounds.center.z);
    }

    private Camera ResolveTargetCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        cachedCamera = Camera.main;
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                cachedCamera = cameras[i];
                return cachedCamera;
            }
        }

        return null;
    }

    private void DestroyVisual()
    {
        if (spawnedMarkVisual != null)
        {
            Destroy(spawnedMarkVisual);
            spawnedMarkVisual = null;
            spawnedMarkRenderer = null;
        }
    }
}
