using UnityEngine;

[DisallowMultipleComponent]
public sealed class RadianceMarkVisualHost : MonoBehaviour
{
    [Header("Radiance Mark")]
    [SerializeField] private Transform radianceMarkAnchor;
    [SerializeField] private Vector3 localPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 localRotationOffset = Vector3.zero;
    [SerializeField] private Vector3 localScale = new Vector3(0.6f, 0.6f, 0.6f);
    [SerializeField] private GameObject defaultMarkVisualPrefab;
    [SerializeField] private bool debugRadianceMarkVisual;

    private GameObject spawnedMarkVisual;
    private SpriteRenderer spawnedMarkRenderer;
    private bool missingAnchorWarningLogged;

    public Transform RadianceMarkAnchor => radianceMarkAnchor;

    public void ShowMark(GameObject markVisualPrefab, Sprite iconSprite, string ownerName = null)
    {
        GameObject resolvedPrefab = markVisualPrefab != null ? markVisualPrefab : defaultMarkVisualPrefab;
        Transform parent = ResolveAnchor(ownerName);
        if (parent == null)
        {
            return;
        }

        if (spawnedMarkVisual == null)
        {
            spawnedMarkVisual = resolvedPrefab != null
                ? Instantiate(resolvedPrefab, parent, false)
                : new GameObject("RadianceMarkVisual");

            spawnedMarkVisual.name = "RadianceMarkVisual";
            spawnedMarkRenderer = spawnedMarkVisual.GetComponentInChildren<SpriteRenderer>(true);
            if (spawnedMarkRenderer == null)
            {
                spawnedMarkRenderer = spawnedMarkVisual.AddComponent<SpriteRenderer>();
            }
        }
        else if (spawnedMarkVisual.transform.parent != parent)
        {
            spawnedMarkVisual.transform.SetParent(parent, false);
        }

        ApplyLocalTransform();
        ApplyIcon(iconSprite);
        spawnedMarkVisual.SetActive(true);

        if (debugRadianceMarkVisual)
        {
            Debug.Log($"[RadianceMarkVisualHost] ShowMark owner={name} anchor={parent.name} visual={(spawnedMarkVisual != null ? spawnedMarkVisual.name : "<null>")}", this);
        }
    }

    public void HideMark(string reason = null)
    {
        if (debugRadianceMarkVisual)
        {
            Debug.Log($"[RadianceMarkVisualHost] HideMark owner={name} reason={(string.IsNullOrWhiteSpace(reason) ? "<none>" : reason)} hasVisual={(spawnedMarkVisual != null)}", this);
        }

        if (spawnedMarkVisual != null)
        {
            Destroy(spawnedMarkVisual);
            spawnedMarkVisual = null;
            spawnedMarkRenderer = null;
        }
    }

    private void OnDisable()
    {
        HideMark("OnDisable");
    }

    private void OnDestroy()
    {
        HideMark("OnDestroy");
    }

    private Transform ResolveAnchor(string ownerName)
    {
        if (radianceMarkAnchor == null)
        {
            if (!missingAnchorWarningLogged)
            {
                missingAnchorWarningLogged = true;
                Debug.LogWarning($"[RadianceMarkVisualHost] Missing radianceMarkAnchor on {name}. Radiance mark visual will not be created. owner={ownerName ?? name}", this);
            }

            return null;
        }

        return radianceMarkAnchor;
    }

    private void ApplyLocalTransform()
    {
        if (spawnedMarkVisual == null)
        {
            return;
        }

        Transform visualTransform = spawnedMarkVisual.transform;
        visualTransform.localPosition = localPositionOffset;
        visualTransform.localRotation = Quaternion.Euler(localRotationOffset);
        visualTransform.localScale = localScale;
    }

    private void ApplyIcon(Sprite iconSprite)
    {
        if (spawnedMarkRenderer == null || iconSprite == null)
        {
            return;
        }

        spawnedMarkRenderer.sprite = iconSprite;
    }
}
