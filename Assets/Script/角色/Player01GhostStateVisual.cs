using Spine.Unity;
using UnityEngine;

public class Player01GhostStateVisual : MonoBehaviour
{
    [Header("Ghost Body")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Material bodyGhostMaterial;
    [SerializeField] private Color bodyTintColor = new Color(0.35f, 0.85f, 1f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float bodyAlpha = 0.35f;
    [SerializeField] private Color flowColor = new Color(0.82f, 0.97f, 1f, 1f);
    [SerializeField, Min(0f)] private float flowIntensity = 1.35f;
    [SerializeField] private float flowSpeedX = 0.35f;
    [SerializeField] private float flowSpeedY = 0.08f;
    [SerializeField, Range(0.01f, 1f)] private float flowWidth = 0.22f;
    [Range(0f, 1f)]
    [SerializeField] private float scanlineIntensity = 0.08f;
    [SerializeField, Min(0f)] private float scanlineDensity = 180f;
    [SerializeField] private float scanlineSpeed = 1.2f;
    [Range(0f, 2f)]
    [SerializeField] private float rgbSplitStrength = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] private float jitterStrength = 0.15f;
    [SerializeField] private float jitterSpeed = 2.2f;
    [Range(0f, 1f)]
    [SerializeField] private float hideNoiseStrength = 0.05f;
    [SerializeField] private float hideNoiseSpeed = 0.8f;

    private Material[] originalBodyMaterials;
    private Material[] activeBodyGhostMaterials;
    private bool ghostActive;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureOriginalMaterials();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureOriginalMaterials();
        if (ghostActive)
        {
            ApplyGhostMaterials();
        }
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        RestoreMaterials();
        DestroyActiveGhostMaterials();
    }

    private void OnDestroy()
    {
        RestoreMaterials();
        DestroyActiveGhostMaterials();
    }

    public void SetGhostActive(bool active)
    {
        CacheReferences();
        CaptureOriginalMaterials();

        if (ghostActive == active)
        {
            return;
        }

        ghostActive = active;

        if (active)
        {
            ApplyGhostMaterials();
        }
        else
        {
            RestoreMaterials();
        }
    }

    private void CacheReferences()
    {
        if (bodyRenderer == null)
        {
            SkeletonAnimation spine = GetComponentInChildren<SkeletonAnimation>(true);
            if (spine != null)
            {
                bodyRenderer = spine.GetComponent<MeshRenderer>();
            }
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void CaptureOriginalMaterials()
    {
        if (bodyRenderer != null && originalBodyMaterials == null)
        {
            originalBodyMaterials = bodyRenderer.sharedMaterials;
        }
    }

    private void ApplyGhostMaterials()
    {
        DestroyActiveGhostMaterials();

        if (bodyRenderer == null || bodyGhostMaterial == null)
        {
            return;
        }

        activeBodyGhostMaterials = CreateMaterialCopies(bodyRenderer.sharedMaterials, bodyGhostMaterial);
        bodyRenderer.materials = activeBodyGhostMaterials;
    }

    private void RestoreMaterials()
    {
        if (bodyRenderer != null && originalBodyMaterials != null)
        {
            bodyRenderer.sharedMaterials = originalBodyMaterials;
        }
    }

    private Material[] CreateMaterialCopies(Material[] sourceMaterials, Material template)
    {
        if (sourceMaterials == null || sourceMaterials.Length == 0 || template == null)
        {
            return sourceMaterials;
        }

        Material[] result = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material baseSource = sourceMaterials[i] != null ? sourceMaterials[i] : template;
            Material copy = new Material(baseSource);
            copy.shader = template.shader;
            ApplyGhostMaterialParameters(copy);
            CopyScanPatternProperties(template, copy);
            result[i] = copy;
        }

        return result;
    }

    private void CopyScanPatternProperties(Material source, Material target)
    {
        if (source == null || target == null)
        {
            return;
        }

        if (source.HasProperty("_ScanPatternTex") && target.HasProperty("_ScanPatternTex"))
        {
            target.SetTexture("_ScanPatternTex", source.GetTexture("_ScanPatternTex"));
        }

        if (source.HasProperty("_ScanPatternStrength") && target.HasProperty("_ScanPatternStrength"))
        {
            target.SetFloat("_ScanPatternStrength", source.GetFloat("_ScanPatternStrength"));
        }

        if (source.HasProperty("_ScanPatternSpeedX") && target.HasProperty("_ScanPatternSpeedX"))
        {
            target.SetFloat("_ScanPatternSpeedX", source.GetFloat("_ScanPatternSpeedX"));
        }

        if (source.HasProperty("_ScanPatternSpeedY") && target.HasProperty("_ScanPatternSpeedY"))
        {
            target.SetFloat("_ScanPatternSpeedY", source.GetFloat("_ScanPatternSpeedY"));
        }

        if (source.HasProperty("_ScanPatternTilingX") && target.HasProperty("_ScanPatternTilingX"))
        {
            target.SetFloat("_ScanPatternTilingX", source.GetFloat("_ScanPatternTilingX"));
        }

        if (source.HasProperty("_ScanPatternTilingY") && target.HasProperty("_ScanPatternTilingY"))
        {
            target.SetFloat("_ScanPatternTilingY", source.GetFloat("_ScanPatternTilingY"));
        }

        if (source.HasProperty("_ScanPatternColor") && target.HasProperty("_ScanPatternColor"))
        {
            target.SetColor("_ScanPatternColor", source.GetColor("_ScanPatternColor"));
        }
    }

    private void ApplyGhostMaterialParameters(Material mat)
    {
        if (mat == null)
        {
            return;
        }

        mat.SetColor("_BodyTintColor", bodyTintColor);
        mat.SetFloat("_BodyAlpha", bodyAlpha);
        mat.SetColor("_FlowColor", flowColor);
        mat.SetFloat("_FlowIntensity", flowIntensity);
        mat.SetFloat("_FlowSpeedX", flowSpeedX);
        mat.SetFloat("_FlowSpeedY", flowSpeedY);
        mat.SetFloat("_FlowWidth", flowWidth);
        mat.SetFloat("_ScanlineIntensity", scanlineIntensity);
        mat.SetFloat("_ScanlineDensity", scanlineDensity);
        mat.SetFloat("_ScanlineSpeed", scanlineSpeed);
        mat.SetFloat("_RGBSplitStrength", rgbSplitStrength);
        mat.SetFloat("_JitterStrength", jitterStrength);
        mat.SetFloat("_JitterSpeed", jitterSpeed);
        mat.SetFloat("_HideNoiseStrength", hideNoiseStrength);
        mat.SetFloat("_HideNoiseSpeed", hideNoiseSpeed);
    }

    private void DestroyActiveGhostMaterials()
    {
        if (activeBodyGhostMaterials == null)
        {
            return;
        }

        for (int i = 0; i < activeBodyGhostMaterials.Length; i++)
        {
            if (activeBodyGhostMaterials[i] != null)
            {
                Destroy(activeBodyGhostMaterials[i]);
            }
        }

        activeBodyGhostMaterials = null;
    }
}
