using UnityEngine;
using Spine.Unity;

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

    [Header("Ghost Shadow")]
    [SerializeField] private Transform shadowRoot;
    [SerializeField] private Renderer shadowRenderer;
    [SerializeField] private Material shadowGhostMaterial;
    [SerializeField] private Vector3 shadowOffset = new Vector3(0.06f, -0.04f, -0.01f);
    [Range(0f, 1f)]
    [SerializeField] private float shadowAlpha = 0.18f;
    [SerializeField] private Color shadowTintColor = new Color(0.18f, 0.45f, 0.82f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float shadowNoiseStrength = 0.08f;
    [Range(0f, 5f)]
    [SerializeField] private float shadowFlowStrength = 0.65f;
    [Range(0f, 2f)]
    [SerializeField] private float shadowRGBSplitStrength = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float shadowJitterStrength = 0.28f;

    private Material[] originalBodyMaterials;
    private Material[] originalShadowMaterials;
    private Material[] activeBodyGhostMaterials;
    private Material[] activeShadowGhostMaterials;
    private Vector3 shadowBaseLocalPosition;
    private bool shadowBaseCaptured;
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
            ApplyShadowOffset();
        }
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void LateUpdate()
    {
        if (ghostActive)
        {
            ApplyShadowOffset();
        }
    }

    private void OnDisable()
    {
        RestoreMaterials();
        RestoreShadowTransform();
        DestroyActiveGhostMaterials();
    }

    private void OnDestroy()
    {
        RestoreMaterials();
        RestoreShadowTransform();
        DestroyActiveGhostMaterials();
    }

    public void SetGhostActive(bool active)
    {
        CacheReferences();
        CaptureOriginalMaterials();

        if (ghostActive == active)
        {
            if (active)
            {
                ApplyShadowOffset();
            }

            return;
        }

        ghostActive = active;

        if (active)
        {
            ApplyGhostMaterials();
            if (shadowRoot != null)
            {
                shadowRoot.gameObject.SetActive(shadowRenderer != null);
            }

            ApplyShadowOffset();
        }
        else
        {
            RestoreMaterials();
            RestoreShadowTransform();
            if (shadowRoot != null)
            {
                shadowRoot.gameObject.SetActive(false);
            }
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

        if (shadowRoot == null)
        {
            Transform candidate = transform.Find("GhostShadowLayer");
            if (candidate != null)
            {
                shadowRoot = candidate;
            }
        }

        if (shadowRenderer == null && shadowRoot != null)
        {
            shadowRenderer = shadowRoot.GetComponentInChildren<Renderer>(true);
        }

        if (!shadowBaseCaptured && shadowRoot != null)
        {
            shadowBaseLocalPosition = shadowRoot.localPosition;
            shadowBaseCaptured = true;
        }
    }

    private void CaptureOriginalMaterials()
    {
        if (bodyRenderer != null && originalBodyMaterials == null)
        {
            originalBodyMaterials = bodyRenderer.sharedMaterials;
        }

        if (shadowRenderer != null && originalShadowMaterials == null)
        {
            originalShadowMaterials = shadowRenderer.sharedMaterials;
        }
    }

    private void ApplyGhostMaterials()
    {
        DestroyActiveGhostMaterials();

        if (bodyRenderer != null)
        {
            if (bodyGhostMaterial != null)
            {
                activeBodyGhostMaterials = CreateMaterialCopies(bodyRenderer.sharedMaterials, bodyGhostMaterial, false);
                bodyRenderer.materials = activeBodyGhostMaterials;
            }
        }

        if (shadowRenderer != null && shadowGhostMaterial != null)
        {
            activeShadowGhostMaterials = CreateMaterialCopies(shadowRenderer.sharedMaterials, shadowGhostMaterial, true);
            shadowRenderer.materials = activeShadowGhostMaterials;
        }
    }

    private void RestoreMaterials()
    {
        if (bodyRenderer != null && originalBodyMaterials != null)
        {
            bodyRenderer.sharedMaterials = originalBodyMaterials;
        }

        if (shadowRenderer != null && originalShadowMaterials != null)
        {
            shadowRenderer.sharedMaterials = originalShadowMaterials;
        }
    }

    private Material[] CreateMaterialCopies(Material[] sourceMaterials, Material template, bool shadow)
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
            ApplyGhostMaterialParameters(copy, shadow);
            result[i] = copy;
        }

        return result;
    }

    private void ApplyGhostMaterialParameters(Material mat, bool shadow)
    {
        if (mat == null)
        {
            return;
        }

        if (shadow)
        {
            mat.SetColor("_BodyTintColor", shadowTintColor);
            mat.SetFloat("_BodyAlpha", shadowAlpha);
            mat.SetColor("_FlowColor", shadowTintColor);
            mat.SetFloat("_FlowIntensity", shadowFlowStrength);
            mat.SetFloat("_FlowSpeedX", 0.22f);
            mat.SetFloat("_FlowSpeedY", 0.05f);
            mat.SetFloat("_FlowWidth", 0.16f);
            mat.SetFloat("_ScanlineIntensity", 0.05f);
            mat.SetFloat("_ScanlineDensity", 120f);
            mat.SetFloat("_ScanlineSpeed", 0.8f);
            mat.SetFloat("_RGBSplitStrength", shadowRGBSplitStrength);
            mat.SetFloat("_JitterStrength", shadowJitterStrength);
            mat.SetFloat("_JitterSpeed", 1.4f);
            mat.SetFloat("_HideNoiseStrength", shadowNoiseStrength);
            mat.SetFloat("_HideNoiseSpeed", 0.65f);
        }
        else
        {
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

            mat.SetFloat("_ShadowAlpha", shadowAlpha);
            mat.SetColor("_ShadowTintColor", shadowTintColor);
            mat.SetFloat("_ShadowOffsetX", shadowOffset.x);
            mat.SetFloat("_ShadowOffsetY", shadowOffset.y);
            mat.SetFloat("_ShadowNoiseStrength", shadowNoiseStrength);
            mat.SetFloat("_ShadowFlowStrength", shadowFlowStrength);
            mat.SetFloat("_ShadowRGBSplitStrength", shadowRGBSplitStrength);
            mat.SetFloat("_ShadowJitterStrength", shadowJitterStrength);
        }
    }

    private void ApplyShadowOffset()
    {
        if (shadowRoot == null || !shadowBaseCaptured)
        {
            return;
        }

        shadowRoot.localPosition = shadowBaseLocalPosition + shadowOffset;
    }

    private void RestoreShadowTransform()
    {
        if (shadowRoot == null || !shadowBaseCaptured)
        {
            return;
        }

        shadowRoot.localPosition = shadowBaseLocalPosition;
    }

    private void DestroyActiveGhostMaterials()
    {
        if (activeBodyGhostMaterials != null)
        {
            for (int i = 0; i < activeBodyGhostMaterials.Length; i++)
            {
                if (activeBodyGhostMaterials[i] != null)
                {
                    Destroy(activeBodyGhostMaterials[i]);
                }
            }

            activeBodyGhostMaterials = null;
        }

        if (activeShadowGhostMaterials != null)
        {
            for (int i = 0; i < activeShadowGhostMaterials.Length; i++)
            {
                if (activeShadowGhostMaterials[i] != null)
                {
                    Destroy(activeShadowGhostMaterials[i]);
                }
            }

            activeShadowGhostMaterials = null;
        }
    }
}
