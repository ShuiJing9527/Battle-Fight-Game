using UnityEngine;

public class WorldHealthBar : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 1.4f, 0f);
    public Vector2 size = new Vector2(1.4f, 0.12f);
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    public Color fillColor = new Color(0.9f, 0.15f, 0.12f, 0.95f);
    public int sortingOrder = 200;

    private static Sprite whiteSprite;

    private CombatHealth combatHealth;
    private EnemyHealth legacyHealth;
    private Transform cameraTransform;
    private GameObject root;
    private Transform fill;
    private int legacyMaxHp;

    private void Awake()
    {
        combatHealth = GetComponent<CombatHealth>();
        legacyHealth = GetComponent<EnemyHealth>();
        legacyMaxHp = legacyHealth != null ? Mathf.Max(1, legacyHealth.hp) : 1;
        CreateBar();
    }

    private void LateUpdate()
    {
        if (root == null)
        {
            return;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        root.transform.position = transform.position + offset;
        if (cameraTransform != null)
        {
            root.transform.rotation = cameraTransform.rotation;
        }

        float ratio = ResolveHealthRatio();
        fill.localScale = new Vector3(size.x * ratio, size.y, 1f);
        fill.localPosition = new Vector3((ratio - 1f) * size.x * 0.5f, 0f, -0.01f);
    }

    private float ResolveHealthRatio()
    {
        if (combatHealth != null)
        {
            return combatHealth.MaxHealthValue > 0f ? Mathf.Clamp01(combatHealth.currentHealth / combatHealth.MaxHealthValue) : 0f;
        }

        if (legacyHealth != null)
        {
            return Mathf.Clamp01((float)legacyHealth.hp / Mathf.Max(1, legacyMaxHp));
        }

        return 1f;
    }

    private void CreateBar()
    {
        root = new GameObject("WorldHealthBar");
        root.transform.SetParent(transform, false);

        GameObject background = CreateSpritePart("Background", backgroundColor, sortingOrder);
        background.transform.localScale = new Vector3(size.x, size.y, 1f);

        GameObject fillObject = CreateSpritePart("Fill", fillColor, sortingOrder + 1);
        fillObject.transform.localScale = new Vector3(size.x, size.y, 1f);
        fillObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        fill = fillObject.transform;
    }

    private GameObject CreateSpritePart(string partName, Color color, int order)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(root.transform, false);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return part;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeHealthBarWhite";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "RuntimeHealthBarWhiteSprite";
        return whiteSprite;
    }
}
