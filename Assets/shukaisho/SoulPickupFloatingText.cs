using TMPro;
using UnityEngine;

public class SoulPickupFloatingText : MonoBehaviour
{
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float riseDistance = 1f;
    [SerializeField] private TextMeshPro textMesh;

    private Vector3 startPosition;
    private Color baseColor;
    private float spawnTime;

    public static void SpawnFallback(string message, Vector3 worldPosition, Color color)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        GameObject popup = new GameObject("SoulPickupFloatingText");
        popup.transform.position = worldPosition;

        SoulPickupFloatingText floatingText = popup.AddComponent<SoulPickupFloatingText>();
        TextMeshPro runtimeText = popup.AddComponent<TextMeshPro>();
        runtimeText.fontSize = 4f;
        runtimeText.alignment = TextAlignmentOptions.Center;
        runtimeText.enableWordWrapping = false;
        runtimeText.raycastTarget = false;
        runtimeText.outlineWidth = 0.15f;
        runtimeText.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            runtimeText.font = defaultFont;
        }

        floatingText.textMesh = runtimeText;
        floatingText.Show(message, color);
    }

    public void Show(string message, Color color)
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshPro>(true);
        }

        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
            textMesh.fontSize = 4f;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.enableWordWrapping = false;
            textMesh.raycastTarget = false;
            textMesh.outlineWidth = 0.15f;
            textMesh.outlineColor = new Color(0f, 0f, 0f, 0.75f);

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                textMesh.font = defaultFont;
            }
        }

        textMesh.text = message;
        textMesh.color = color;

        startPosition = transform.position;
        baseColor = color;
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (textMesh == null)
        {
            Destroy(gameObject);
            return;
        }

        float elapsed = Time.time - spawnTime;
        float normalized = lifetime <= 0f ? 1f : Mathf.Clamp01(elapsed / lifetime);

        transform.position = startPosition + Vector3.up * (riseDistance * normalized);

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            transform.forward = activeCamera.transform.forward;
        }

        Color color = baseColor;
        color.a *= 1f - normalized;
        textMesh.color = color;

        if (normalized >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
