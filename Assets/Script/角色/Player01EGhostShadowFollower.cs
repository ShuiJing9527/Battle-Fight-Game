using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

public class Player01EGhostShadowFollower : MonoBehaviour
{
    [SerializeField] private Transform sourceRoot;
    [SerializeField] private SkeletonAnimation sourceSkeleton;
    [SerializeField] private Transform shadowRoot;
    [SerializeField] private Transform shadowSpineRoot;
    [SerializeField] private SkeletonAnimation shadowSkeleton;
    [SerializeField] private Renderer shadowRenderer;
    [SerializeField] private Material shadowMaterial;

    [SerializeField] private float followDistance = 0.45f;
    [SerializeField] private float followSmooth = 8f;
    [SerializeField] private Vector3 idleOffset = new Vector3(-0.25f, 0f, 0.02f);
    [SerializeField] private Vector3 shadowSpineLocalOffset = new Vector3(0f, -0.8f, 0f);
    [SerializeField] private int sortingOrderOffset = -2;

    private Vector3 initialShadowLocalPosition;
    private Quaternion initialShadowLocalRotation;
    private Vector3 initialShadowLocalScale;
    private bool hasInitialShadowTransform;
    private bool shadowActive;
    private bool hasCachedPreviousSourcePosition;
    private Vector3 previousSourceWorldPosition;
    private string lastAnimationName;
    private bool lastAnimationLoop;

    private void Awake()
    {
        CacheReferences();
        CaptureInitialShadowTransform();
        RestoreShadowTransform();
        SetShadowActive(false);
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureInitialShadowTransform();
        if (shadowActive)
        {
            RestoreShadowTransform();
            ApplyShadowState();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            SetShadowActive(!shadowActive);
        }
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void LateUpdate()
    {
        if (!shadowActive)
        {
            return;
        }

        CacheReferences();
        UpdateFollowTransform();
        ApplySpineLocalOffset();
        SyncAnimationAndFacing();
        EnsureShadowMaterialApplied();
    }

    private void OnDisable()
    {
        SetShadowActive(false);
    }

    private void OnDestroy()
    {
        SetShadowActive(false);
    }

    public void SetShadowActive(bool active)
    {
        CacheReferences();
        CaptureInitialShadowTransform();

        if (shadowActive == active)
        {
            if (active)
            {
                RestoreShadowTransform();
                ApplyShadowState();
                LogShadowState("TRUE");
            }
            else if (shadowRoot != null)
            {
                shadowRoot.gameObject.SetActive(false);
                RestoreShadowTransform();
                LogShadowState("FALSE");
            }

            return;
        }

        shadowActive = active;

        if (active)
        {
            if (shadowRoot != null)
            {
                shadowRoot.gameObject.SetActive(true);
            }

            if (shadowSpineRoot != null)
            {
                shadowSpineRoot.gameObject.SetActive(true);
            }

            RestoreShadowTransform();
            ApplySpineLocalOffset();
            hasCachedPreviousSourcePosition = false;
            ApplyShadowState();
            UpdateFollowTransform();
            SyncAnimationAndFacing();
            EnsureShadowMaterialApplied();
            LogShadowState("TRUE");
        }
        else
        {
            if (shadowRoot != null)
            {
                shadowRoot.gameObject.SetActive(false);
            }

            if (shadowSpineRoot != null)
            {
                shadowSpineRoot.gameObject.SetActive(false);
            }

            RestoreShadowTransform();
            lastAnimationName = null;
            lastAnimationLoop = false;
            hasCachedPreviousSourcePosition = false;
            LogShadowState("FALSE");
        }
    }

    public void Reinitialize()
    {
        CacheReferences();
        CaptureInitialShadowTransform();
        RestoreShadowTransform();
        hasCachedPreviousSourcePosition = false;
        lastAnimationName = null;
        lastAnimationLoop = false;

        if (shadowActive)
        {
            ApplyShadowState();
            ApplySpineLocalOffset();
            UpdateFollowTransform();
            SyncAnimationAndFacing();
            EnsureShadowMaterialApplied();
        }
    }

    private void CacheReferences()
    {
        if (sourceRoot == null)
        {
            sourceRoot = transform.parent;
        }

        if (sourceSkeleton == null)
        {
            if (sourceRoot != null)
            {
                sourceSkeleton = sourceRoot.GetComponentInChildren<SkeletonAnimation>(true);
            }
            else
            {
                sourceSkeleton = GetComponentInParent<SkeletonAnimation>(true);
            }
        }

        if (shadowRoot == null)
        {
            shadowRoot = transform;
        }

        if (shadowSpineRoot == null && shadowSkeleton != null)
        {
            shadowSpineRoot = shadowSkeleton.transform;
        }

        if (shadowSpineRoot == null && shadowRoot != null)
        {
            Transform[] children = shadowRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == shadowRoot)
                {
                    continue;
                }

                shadowSpineRoot = child;
                break;
            }
        }

        if (shadowSkeleton == null && shadowRoot != null)
        {
            if (shadowSpineRoot != null)
            {
                shadowSkeleton = shadowSpineRoot.GetComponent<SkeletonAnimation>();
            }
            else
            {
                shadowSkeleton = shadowRoot.GetComponent<SkeletonAnimation>();
            }
        }

        if (shadowRenderer == null)
        {
            if (shadowSpineRoot != null)
            {
                shadowRenderer = shadowSpineRoot.GetComponent<Renderer>();
            }
            else if (shadowRoot != null)
            {
                shadowRenderer = shadowRoot.GetComponent<Renderer>();
            }
        }
    }

    private void CaptureInitialShadowTransform()
    {
        if (hasInitialShadowTransform || shadowRoot == null)
        {
            return;
        }

        initialShadowLocalPosition = shadowRoot.localPosition;
        initialShadowLocalRotation = shadowRoot.localRotation;
        initialShadowLocalScale = shadowRoot.localScale;
        hasInitialShadowTransform = true;
    }

    private void RestoreShadowTransform()
    {
        if (!hasInitialShadowTransform || shadowRoot == null)
        {
            return;
        }

        shadowRoot.localPosition = initialShadowLocalPosition;
        shadowRoot.localRotation = initialShadowLocalRotation;
        shadowRoot.localScale = initialShadowLocalScale;
    }

    private void ApplyShadowState()
    {
        if (shadowRenderer != null)
        {
            if (shadowMaterial != null)
            {
                ApplyShadowMaterial();
            }

            if (sourceSkeleton != null && sourceSkeleton.GetComponent<Renderer>() != null)
            {
                Renderer sourceRenderer = sourceSkeleton.GetComponent<Renderer>();
                shadowRenderer.sortingLayerName = sourceRenderer.sortingLayerName;
                shadowRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            }
        }

        if (shadowRoot != null)
        {
            Renderer rootRenderer = shadowRoot.GetComponent<Renderer>();
            if (rootRenderer != null && rootRenderer != shadowRenderer)
            {
                rootRenderer.enabled = false;
            }

            SkeletonAnimation rootSkeleton = shadowRoot.GetComponent<SkeletonAnimation>();
            if (rootSkeleton != null && rootSkeleton != shadowSkeleton)
            {
                rootSkeleton.enabled = false;
            }
        }

        if (shadowSkeleton != null && sourceSkeleton != null)
        {
            shadowSkeleton.Initialize(false);
            if (shadowSkeleton.Skeleton != null && sourceSkeleton.Skeleton != null)
            {
                shadowSkeleton.Skeleton.ScaleX = sourceSkeleton.Skeleton.ScaleX;
                shadowSkeleton.Skeleton.ScaleY = sourceSkeleton.Skeleton.ScaleY;
            }
        }

        ApplyShadowMaterial();
        ApplySpineLocalOffset();
    }

    private void LogShadowState(string state)
    {
        Debug.Log($"[E Shadow] SetShadowActive {state}", this);
        Debug.Log($"[E Shadow] shadowRoot active = {(shadowRoot != null && shadowRoot.gameObject.activeSelf)}", this);
        Debug.Log($"[E Shadow] shadowSkeleton active = {(shadowSkeleton != null && shadowSkeleton.gameObject.activeSelf)}", this);
        Debug.Log($"[E Shadow] shadowRenderer = {(shadowRenderer != null ? shadowRenderer.name : "null")}", this);
    }

    private void UpdateFollowTransform()
    {
        if (shadowRoot == null || sourceRoot == null)
        {
            return;
        }

        Vector3 sourcePosition = sourceRoot.position;
        if (!hasCachedPreviousSourcePosition)
        {
            previousSourceWorldPosition = sourcePosition;
            hasCachedPreviousSourcePosition = true;
        }

        Vector3 delta = sourcePosition - previousSourceWorldPosition;
        previousSourceWorldPosition = sourcePosition;

        Vector3 localDelta = sourceRoot.InverseTransformDirection(delta);
        Vector3 targetLocalOffset = idleOffset;
        if (Mathf.Abs(localDelta.x) > 0.0005f)
        {
            targetLocalOffset = localDelta.x > 0f
                ? idleOffset + new Vector3(-followDistance, 0f, 0f)
                : idleOffset + new Vector3(followDistance, 0f, 0f);
        }

        float lerpT = Mathf.Clamp01(Time.deltaTime * followSmooth);
        shadowRoot.localPosition = Vector3.Lerp(shadowRoot.localPosition, targetLocalOffset, lerpT);
    }

    private void ApplySpineLocalOffset()
    {
        if (shadowSpineRoot == null)
        {
            return;
        }

        shadowSpineRoot.localPosition = shadowSpineLocalOffset;
    }

    private void ApplyShadowMaterial()
    {
        if (shadowRenderer == null || shadowMaterial == null)
        {
            return;
        }

        Material[] sharedMaterials = shadowRenderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            sharedMaterials = new Material[1];
        }

        if (shadowSkeleton != null && shadowSkeleton.CustomMaterialOverride != null)
        {
            shadowSkeleton.CustomMaterialOverride.Clear();
        }

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material originalMaterial = sharedMaterials[i];
            sharedMaterials[i] = shadowMaterial;

            if (shadowSkeleton != null && shadowSkeleton.CustomMaterialOverride != null && originalMaterial != null)
            {
                shadowSkeleton.CustomMaterialOverride[originalMaterial] = shadowMaterial;
            }
        }

        shadowRenderer.sharedMaterials = sharedMaterials;

        Debug.Log($"[E Shadow] Apply shadow material = {shadowMaterial.name}", this);
        Debug.Log($"[E Shadow] shadowRenderer material after apply = {(shadowRenderer.sharedMaterial != null ? shadowRenderer.sharedMaterial.name : "null")}", this);
        Debug.Log($"[E Shadow] shadowSkeleton = {(shadowSkeleton != null ? shadowSkeleton.name : "null")}", this);
        Debug.Log($"[E Shadow] shadowRoot active = {(shadowRoot != null && shadowRoot.gameObject.activeSelf)}", this);

        if (shadowRenderer.sharedMaterial != shadowMaterial && shadowSkeleton != null)
        {
            Debug.LogWarning("[E Shadow] shadowRenderer is still not using shadow material after apply.", this);
        }
        else if (shadowRenderer.sharedMaterial == shadowMaterial)
        {
            Debug.Log("[E Shadow] shadowRenderer now uses shadow material.", this);
        }
    }

    private void EnsureShadowMaterialApplied()
    {
        if (shadowRenderer == null || shadowMaterial == null)
        {
            return;
        }

        Material[] sharedMaterials = shadowRenderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            ApplyShadowMaterial();
            return;
        }

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            if (sharedMaterials[i] != shadowMaterial)
            {
                ApplyShadowMaterial();
                return;
            }
        }

        if (shadowRenderer.sharedMaterial != shadowMaterial)
        {
            ApplyShadowMaterial();
        }
    }

    private void SyncAnimationAndFacing()
    {
        if (shadowSkeleton == null || sourceSkeleton == null || sourceSkeleton.AnimationState == null)
        {
            return;
        }

        TrackEntry sourceTrack = sourceSkeleton.AnimationState.GetCurrent(0);
        string animationName = sourceTrack != null && sourceTrack.Animation != null
            ? sourceTrack.Animation.Name
            : sourceSkeleton.AnimationName;
        bool loop = sourceTrack != null && sourceTrack.Loop;

        if (shadowSkeleton.Skeleton != null && sourceSkeleton.Skeleton != null)
        {
            shadowSkeleton.Skeleton.ScaleX = sourceSkeleton.Skeleton.ScaleX;
            shadowSkeleton.Skeleton.ScaleY = sourceSkeleton.Skeleton.ScaleY;
        }

        if (string.IsNullOrEmpty(animationName))
        {
            return;
        }

        if (animationName == lastAnimationName && loop == lastAnimationLoop)
        {
            return;
        }

        shadowSkeleton.AnimationState.SetAnimation(0, animationName, loop);
        lastAnimationName = animationName;
        lastAnimationLoop = loop;
    }
}
