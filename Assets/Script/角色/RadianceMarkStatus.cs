using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class RadianceMarkStatus : MonoBehaviour
{
    private const string DefaultMarkVisualPrefabAssetPath = "Assets/Prefabs/UI/StatusMarks/RadianceMarkIcon.prefab";
    private const string DefaultMarkIconSpriteAssetPath = "Assets/Prefabs/UI/DayNightAffinity/Textures/Icon_Radiance_Sun.png";

    private static readonly HashSet<RadianceMarkStatus> ActiveStatuses = new HashSet<RadianceMarkStatus>();
    private static readonly List<RadianceMarkStatus> CleanupBuffer = new List<RadianceMarkStatus>();
    private static long nextAppliedSequence = 1L;

    [SerializeField, Min(0f)] private float remainingDuration;
    [SerializeField] private long appliedSequence;
    [SerializeField] private bool isMarked;
    [SerializeField] private bool debugLifecycle;

    [SerializeField] private GameObject markVisualPrefab;
    [SerializeField] private Sprite markIconSprite;

    private CombatHealth cachedCombatHealth;
    private RadianceMarkVisualHost cachedVisualHost;
    private bool consumeRequested;
    private bool eventsBound;
    private bool isClearing;
    private bool missingVisualHostWarningLogged;

    public bool IsActive => remainingDuration > 0f && IsValidMarkTarget(cachedCombatHealth);
    public bool IsMarked => isMarked && remainingDuration > 0f && IsValidMarkTarget(cachedCombatHealth);
    public float RemainingDuration => Mathf.Max(0f, remainingDuration);
    public long AppliedSequence => appliedSequence;

    public static int ActiveMarkCount
    {
        get
        {
            CleanupInvalidActiveMarks();
            return ActiveStatuses.Count;
        }
    }

    private void Awake()
    {
        CacheCombatHealth();
    }

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
    }

    public void SetDebugLifecycle(bool enabled)
    {
        debugLifecycle = enabled;
    }

    public void ApplyOrRefresh(float duration)
    {
        bool wasMarked = IsMarked;
        float resolvedDuration = Mathf.Max(0f, duration);
        if (resolvedDuration <= 0f)
        {
            DebugLifecycle($"ApplyOrRefresh skipped reason=duration<=0 duration={duration:F2}");
            ClearMark(removeComponent: false);
            return;
        }

        CacheCombatHealth();
        if (!IsValidMarkTarget(cachedCombatHealth))
        {
            DebugLifecycle($"ApplyOrRefresh skipped reason=invalid-target duration={duration:F2}");
            ClearMark(removeComponent: false);
            return;
        }

        BindLifecycleEvents();

        remainingDuration = resolvedDuration;
        appliedSequence = nextAppliedSequence++;
        isMarked = true;
        consumeRequested = false;
        enabled = true;
        RegisterActive();
        ShowVisual();
        DebugLifecycle($"{(wasMarked ? "RefreshMark" : "ApplyMark")} duration={resolvedDuration:F2} sequence={appliedSequence}");
    }

    public static void ApplyOrRefresh(GameObject target, float duration, GameObject visualPrefab = null, Sprite iconSprite = null, bool debugLifecycle = false)
    {
        if (target == null)
        {
            return;
        }

        CombatHealth combatHealth = target.GetComponent<CombatHealth>();
        if (!IsValidMarkTarget(combatHealth))
        {
            return;
        }

        RadianceMarkStatus status = target.GetComponent<RadianceMarkStatus>();
        if (status == null)
        {
            status = target.AddComponent<RadianceMarkStatus>();
        }

        status.SetDebugLifecycle(debugLifecycle);
        status.ConfigureVisual(visualPrefab, iconSprite);
        status.ApplyOrRefresh(duration);
    }

    public static bool TryGetMarkedStatus(CombatHealth combatHealth, out RadianceMarkStatus status)
    {
        status = null;
        if (!IsValidMarkTarget(combatHealth))
        {
            return false;
        }

        status = combatHealth.GetComponent<RadianceMarkStatus>();
        if (status == null || !status.IsMarked)
        {
            status = null;
            return false;
        }

        return true;
    }

    public static bool TryGetActiveStatus(CombatHealth combatHealth, out RadianceMarkStatus status)
    {
        return TryGetMarkedStatus(combatHealth, out status);
    }

    public static bool IsValidMarkTarget(CombatHealth combatHealth)
    {
        if (combatHealth == null || combatHealth.gameObject == null)
        {
            return false;
        }

        GameObject target = combatHealth.gameObject;
        if (!target.activeInHierarchy || !target.scene.IsValid())
        {
            return false;
        }

        DissolveOnDeath dissolveOnDeath = combatHealth.GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath != null && dissolveOnDeath.IsDeathStarted)
        {
            return false;
        }

        SlimeAnimationController slimeAnimation = combatHealth.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null && !slimeAnimation.IsVisualPresentationVisible)
        {
            return false;
        }

        return !combatHealth.IsDead && ResolveCurrentHealth(combatHealth) > 0f;
    }

    public static void CleanupInvalidActiveMarks()
    {
        CleanupBuffer.Clear();
        foreach (RadianceMarkStatus status in ActiveStatuses)
        {
            if (status == null || !status.IsMarked)
            {
                CleanupBuffer.Add(status);
            }
        }

        for (int i = 0; i < CleanupBuffer.Count; i++)
        {
            RadianceMarkStatus status = CleanupBuffer[i];
            if (status == null)
            {
                ActiveStatuses.Remove(status);
                continue;
            }

            status.ClearMark(removeComponent: false);
        }

        CleanupBuffer.Clear();
    }

    public bool Consume()
    {
        if (consumeRequested || !IsMarked)
        {
            DebugLifecycle("Consume skipped reason=not-marked");
            return false;
        }

        consumeRequested = true;
        DebugLifecycle("Consume");
        ClearMark(removeComponent: true);
        return true;
    }

    public void ForceClear(string reason, bool removeComponent = true)
    {
        DebugLifecycle(BuildLifecycleSnapshot($"ForceClear reason={reason}"));
        ClearMark(removeComponent);
    }

    public void ClearMark(bool removeComponent = false)
    {
        if (isClearing)
        {
            return;
        }

        isClearing = true;
        remainingDuration = 0f;
        appliedSequence = 0L;
        isMarked = false;
        consumeRequested = false;
        UnregisterActive();
        UnbindLifecycleEvents();
        DestroyVisual();
        enabled = false;
        DebugLifecycle($"ClearMark removeComponent={removeComponent}");
        isClearing = false;

        if (removeComponent)
        {
            Destroy(this);
        }
    }

    private void OnEnable()
    {
        if (remainingDuration > 0f)
        {
            CacheCombatHealth();
            if (IsValidMarkTarget(cachedCombatHealth))
            {
                BindLifecycleEvents();
                RegisterActive();
                ShowVisual();
            }
        }
    }

    private void Update()
    {
        if (!IsValidMarkTarget(cachedCombatHealth))
        {
            DebugLifecycle(BuildLifecycleSnapshot("Update invalid-target"));
            ClearMark(removeComponent: true);
            return;
        }

        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (remainingDuration <= 0f)
        {
            ClearMark(removeComponent: true);
        }
    }

    private void OnDisable()
    {
        DebugLifecycle(BuildLifecycleSnapshot("OnDisable"));
        if (isClearing)
        {
            return;
        }

        ClearMark(removeComponent: false);
    }

    private void OnDestroy()
    {
        DebugLifecycle(BuildLifecycleSnapshot("OnDestroy"));
        UnregisterActive();
        UnbindLifecycleEvents();
        DestroyVisual();
    }

    private void HandleCombatHealthDied(GameObject attacker)
    {
        DebugLifecycle(BuildLifecycleSnapshot($"CombatHealth.Died attacker={GetObjectName(attacker)}"));
        ClearMark(removeComponent: true);
    }

    private void CacheCombatHealth()
    {
        if (cachedCombatHealth == null)
        {
            cachedCombatHealth = GetComponent<CombatHealth>();
        }

        if (cachedVisualHost == null)
        {
            cachedVisualHost = GetComponent<RadianceMarkVisualHost>();
        }
    }

    private void BindLifecycleEvents()
    {
        CacheCombatHealth();
        if (eventsBound || cachedCombatHealth == null)
        {
            return;
        }

        cachedCombatHealth.Died += HandleCombatHealthDied;
        eventsBound = true;
    }

    private void UnbindLifecycleEvents()
    {
        if (!eventsBound || cachedCombatHealth == null)
        {
            return;
        }

        cachedCombatHealth.Died -= HandleCombatHealthDied;
        eventsBound = false;
    }

    private void RegisterActive()
    {
        ActiveStatuses.Add(this);
        DebugLifecycle($"RegisterActive activeCount={ActiveStatuses.Count}");
    }

    private void UnregisterActive()
    {
        ActiveStatuses.Remove(this);
        DebugLifecycle($"UnregisterActive activeCount={ActiveStatuses.Count}");
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

    private void ShowVisual()
    {
        EnsureVisualDefaultsAssigned();

        RadianceMarkVisualHost visualHost = ResolveVisualHost();
        if (visualHost == null)
        {
            return;
        }

        visualHost.ShowMark(markVisualPrefab, markIconSprite, gameObject.name);
    }

    private void DestroyVisual()
    {
        RadianceMarkVisualHost visualHost = cachedVisualHost != null ? cachedVisualHost : ResolveVisualHost();
        if (visualHost != null)
        {
            visualHost.HideMark("RadianceMarkStatus.DestroyVisual");
        }
    }

    private RadianceMarkVisualHost ResolveVisualHost()
    {
        CacheCombatHealth();
        if (cachedVisualHost != null)
        {
            return cachedVisualHost;
        }

        if (!missingVisualHostWarningLogged)
        {
            missingVisualHostWarningLogged = true;
            Debug.LogWarning($"[RadianceMark] Missing RadianceMarkVisualHost on target={GetObjectName(gameObject)} id={GetInstanceID()}. Radiance mark visual will not be created.", this);
        }

        return null;
    }

    private static float ResolveCurrentHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return Mathf.Max(0f, combatHealth.resourceBank.currentHealth);
        }

        return Mathf.Max(0f, combatHealth.currentHealth);
    }

    private string BuildLifecycleSnapshot(string reason)
    {
        GameObject target = cachedCombatHealth != null ? cachedCombatHealth.gameObject : gameObject;
        bool targetActive = target != null && target.activeInHierarchy;
        bool combatDead = cachedCombatHealth != null && cachedCombatHealth.IsDead;
        bool hasVisualInstance = cachedVisualHost != null;
        string followTargetName = ResolveFollowTargetName();
        string visualState = ResolveVisualStateText(target);
        return $"reason={reason} targetActive={targetActive} combatDead={combatDead} isMarked={isMarked} visualInstance={hasVisualInstance} followTarget={followTargetName} visualState={visualState}";
    }

    private string ResolveFollowTargetName()
    {
        if (cachedVisualHost != null && cachedVisualHost.RadianceMarkAnchor != null)
        {
            return cachedVisualHost.RadianceMarkAnchor.name;
        }

        SlimeAnimationController slimeAnimation = cachedCombatHealth != null ? cachedCombatHealth.GetComponent<SlimeAnimationController>() : null;
        if (slimeAnimation != null && slimeAnimation.VisualRoot != null)
        {
            return slimeAnimation.VisualRoot.name;
        }

        return cachedCombatHealth != null ? cachedCombatHealth.transform.name : transform.name;
    }

    private static string ResolveVisualStateText(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        SlimeAnimationController slimeAnimation = target.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null)
        {
            Transform visualRoot = slimeAnimation.VisualRoot;
            bool visualRootActive = visualRoot != null && visualRoot.gameObject.activeInHierarchy;
            return $"slimeVisible={slimeAnimation.IsVisualPresentationVisible} visualRootActive={visualRootActive}";
        }

        return "visualState=non-slime";
    }

    private void DebugLifecycle(string message)
    {
        if (!debugLifecycle)
        {
            return;
        }

        Debug.Log($"[RadianceMark] target={GetObjectName(gameObject)} id={GetInstanceID()} marked={isMarked} remaining={remainingDuration:F2} {message}", this);
    }

    private static string GetObjectName(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        return target.name;
    }
}
