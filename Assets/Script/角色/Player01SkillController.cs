using UnityEngine;
using UnityEngine.InputSystem;
using Spine;
using Spine.Unity;

public class Player01SkillController : MonoBehaviour
{
    [Header("Skill Slots")]
    [SerializeField] private Player01SkillBase qSkill;
    [SerializeField] private Player01SkillBase wSkill;
    [SerializeField] private Player01SkillBase eSkill;
    [SerializeField] private Player01SkillBase rSkill;

    [Header("Spine Playback")]
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private string walkAnimationName = "Walk";
    [SerializeField] private string runAnimationName = "Run";
    [SerializeField, Min(0f)] private float walkSpeedThreshold = 0.15f;
    [SerializeField, Min(0f)] private float runSpeedThreshold = 6f;

    [Header("Display Fix")]
    [SerializeField] private int spineSortingOrder = 20;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugSkillCooldownFlow = true;

    [Header("HUD Cooldowns")]
    [SerializeField, Min(0f)] private float qCooldown = 3f;
    [SerializeField, Min(0f)] private float wCooldown = 5f;
    [SerializeField, Min(0f)] private float eCooldown = 8f;
    [SerializeField, Min(0f)] private float rCooldown = 12f;

    private SkeletonAnimation cachedSkeletonAnimation;
    private MeshRenderer cachedSpineRenderer;
    private PlayerMovement cachedMovement;
    private Rigidbody cachedRigidbody;
    private Player01SkillBase currentSkill;
    private string currentLocomotionAnimation;
    private int cachedFacingScaleX = 1;
    private bool skillAnimationLocked;
    private float skillAnimationLockUntil;
    private int lastLocomotionLockLogFrame = -1;
    private bool skillFacingLocked;
    private int lockedFacingScaleX = 1;
    private bool skillMovementFrozen;
    private float frozenMoveSpeed = -1f;
    private int lastFacingLockLogFrame = -1;

    private void Reset()
    {
        CacheReferences();
        AutoBindSkills();
    }

    private void Awake()
    {
        EnsureRuntimeCombatComponents();
        CacheReferences();
        AutoBindSkills();
        InitializeSkills();
        SyncHudCooldownsToResourceManager();
        RestoreLocomotionAnimation(true);
        ApplyDisplayFixes(true);
    }

    private void OnValidate()
    {
        CacheReferences();
        AutoBindSkills();
    }

    private void Update()
    {
        UpdateSkillLockState();
        ApplyFacingFromMovement();

        if (Keyboard.current == null)
        {
            TryRestoreLocomotionAnimation("Update(NoKeyboard)", false);
            return;
        }

        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            ForcePlayAtk2Test();
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (IsVeilBarrierActive())
            {
                Debug.Log("[Player01SkillController] Q pressed while W active", this);
            }
            TryCastSkillFromInput("Q", qSkill);
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            TryCastSkillFromInput("W", wSkill);
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryCastSkillFromInput("E", eSkill);
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            TryCastSkillFromInput("R", rSkill);
        }

        TryRestoreLocomotionAnimation("Update", false);
    }

    private void LateUpdate()
    {
        ApplyDisplayFixes(false);
    }

    public Vector3 GetFacingWorldDirection()
    {
        return cachedFacingScaleX < 0 ? Vector3.right : Vector3.left;
    }

    public int GetFacingMirrorScaleX()
    {
        return cachedFacingScaleX;
    }

    public void InitializeSkills()
    {
        if (qSkill != null) qSkill.Initialize(this);
        if (wSkill != null) wSkill.Initialize(this);
        if (eSkill != null) eSkill.Initialize(this);
        if (rSkill != null) rSkill.Initialize(this);
        SyncHudCooldownsToResourceManager();
    }

    public bool TryBeginSkill(Player01SkillBase skill)
    {
        if (skill == null)
        {
            return false;
        }

        if (currentSkill != null && currentSkill != skill)
        {
            if (debugLog)
            {
                Debug.Log($"[Player01SkillController] Busy with {currentSkill.name}, ignored {skill.name}.", this);
            }

            return false;
        }

        currentSkill = skill;
        return true;
    }

    public void FinishSkill(Player01SkillBase skill)
    {
        if (skill == null || currentSkill != skill)
        {
            return;
        }

        currentSkill = null;

        UpdateSkillLockState();
        TryRestoreLocomotionAnimation("FinishSkill", true);
    }

    public bool TryPlaySkillAnimation(string animationName, bool loop)
    {
        return PlaySkillAnimation(animationName, loop, false, false) != null;
    }

    public TrackEntry PlaySkillAnimation(string animationName, bool loop, bool clearTrackFirst, bool verboseLog)
    {
        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine == null || spine.Skeleton == null || spine.AnimationState == null)
        {
            if (verboseLog)
            {
                Debug.LogWarning($"[Player01SkillController] Cannot play '{animationName}' because SkeletonAnimation is missing on {name}.", this);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(animationName))
        {
            return null;
        }

        if (spine.Skeleton.Data == null || spine.Skeleton.Data.FindAnimation(animationName) == null)
        {
            if (verboseLog || debugLog)
            {
                Debug.LogWarning($"[Player01SkillController] Missing Spine animation '{animationName}' on {name}.", this);
            }

            return null;
        }

        if (verboseLog)
        {
            Debug.Log($"[Player01SkillController] Play '{animationName}' on {spine.name}, track=0, clearTrackFirst={clearTrackFirst}.", this);
        }

        return PlayAnimationEquivalent(spine, animationName, loop, clearTrackFirst, verboseLog, "Player01SkillController");
    }

    public bool ForcePlayAtk2Test()
    {
        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine == null || spine.Skeleton == null || spine.AnimationState == null)
        {
            Debug.LogWarning($"[TEST] Force play ATK2 failed: SkeletonAnimation is missing on {name}.", this);
            return false;
        }

        Debug.Log($"[TEST] Force play ATK2 on {DescribeSkeletonAnimation(spine)}", this);

        if (spine.Skeleton.Data == null || spine.Skeleton.Data.FindAnimation("ATK2") == null)
        {
            Debug.LogWarning($"[TEST] Force play ATK2 failed: animation 'ATK2' not found.", this);
            return false;
        }

        skillAnimationLocked = true;
        skillAnimationLockUntil = Time.time + 1.0f;

        bool played = TryPlayLockedSkillAnimation("ATK2", false, 1.0f, true, "TEST");
        if (!played)
        {
            Debug.LogWarning("[TEST] SetAnimation returned null for ATK2.", this);
            return false;
        }

        TrackEntry entry = ResolveSkeletonAnimation()?.AnimationState?.GetCurrent(0);
        Debug.Log($"[TEST] TrackEntry animation={entry?.Animation?.Name}, track={entry?.TrackIndex}, duration={entry?.Animation?.Duration:F3}, mixDuration={entry?.MixDuration:F3}.", this);
        Debug.Log($"[TEST] Current Track0 animation={GetCurrentTrackAnimationName(0)}", this);

        StartCoroutine(LogTrackAfterOneFrame(spine, "ATK2"));
        return true;
    }

    public void RestoreLocomotionAnimation(bool force = false)
    {
        TryRestoreLocomotionAnimation("RestoreLocomotionAnimation", force);
    }

    private void TryRestoreLocomotionAnimation(string source, bool force)
    {
        bool allowLocomotionWhileRunningBoost =
            currentSkill is Player1Skill_E_BrokenDash eSkill && eSkill.IsRunningBoost;
        bool skillLocksLocomotion = currentSkill != null && currentSkill.LocksLocomotionAnimation();

        if (currentSkill != null && !force && skillLocksLocomotion && !allowLocomotionWhileRunningBoost)
        {
            return;
        }

        if (IsSkillAnimationLocked())
        {
            if (debugLog && lastLocomotionLockLogFrame != Time.frameCount)
            {
                Debug.Log("[Locomotion] skipped because skill animation locked", this);
                lastLocomotionLockLogFrame = Time.frameCount;
            }

            return;
        }

        string animation = ResolveLocomotionAnimationName();
        if (string.IsNullOrWhiteSpace(animation))
        {
            return;
        }

        if (!force && currentLocomotionAnimation == animation)
        {
            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Locomotion] {source} -> {animation}", this);
        }

        SkeletonAnimation spine = ResolveSkeletonAnimation();
        PlayAnimationEquivalent(spine, animation, true, false, false, source);
        currentLocomotionAnimation = animation;
    }

    public void LockSkillAnimation(float lockDuration)
    {
        skillAnimationLocked = true;
        skillAnimationLockUntil = Mathf.Max(skillAnimationLockUntil, Time.time + Mathf.Max(0f, lockDuration));

        if (!skillFacingLocked)
        {
            skillFacingLocked = true;
            lockedFacingScaleX = cachedFacingScaleX;
        }

        if (!skillMovementFrozen && (currentSkill == null || currentSkill == qSkill))
        {
            FreezeMovementDuringSkillLock();
        }

        if (debugLog)
        {
            Debug.Log($"[SkillLock] cached facing scale = {lockedFacingScaleX}, lockUntil={skillAnimationLockUntil:F2}.", this);
        }
    }

    public bool TryPlayLockedSkillAnimation(string animationName, bool loop, float lockDuration)
    {
        return TryPlayLockedSkillAnimation(animationName, loop, lockDuration, false, "Skill");
    }

    public bool TryPlayLockedSkillAnimation(string animationName, bool loop, float lockDuration, bool forceRestart, string source)
    {
        LockSkillAnimation(lockDuration);

        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine == null || spine.Skeleton == null || spine.AnimationState == null)
        {
            Debug.LogWarning($"[{source}] Cannot play '{animationName}' because SkeletonAnimation is missing on {name}.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(animationName))
        {
            Debug.LogWarning($"[{source}] Cannot play empty animation name.", this);
            return false;
        }

        if (spine.Skeleton.Data == null || spine.Skeleton.Data.FindAnimation(animationName) == null)
        {
            Debug.LogWarning($"[{source}] Missing Spine animation '{animationName}' on {name}.", this);
            return false;
        }

        if (forceRestart)
        {
            spine.AnimationState.SetEmptyAnimation(0, 0f);
        }

        TrackEntry entry = PlayAnimationEquivalent(spine, animationName, loop, forceRestart, true, source);
        if (entry == null)
        {
            Debug.LogWarning($"[{source}] Failed to play '{animationName}'.", this);
            return false;
        }

        if (forceRestart)
        {
            entry.MixDuration = 0f;
            entry.TrackTime = 0f;
        }

        Debug.Log($"[{source}] AnimationName after set = {spine.AnimationName}", this);
        return true;
    }

    public void ClearSkillAnimationLock()
    {
        skillAnimationLocked = false;
        skillAnimationLockUntil = 0f;
        skillFacingLocked = false;
        lastFacingLockLogFrame = -1;
        RestoreMovementAfterSkillLock();
    }

    private void CacheReferences()
    {
        if (cachedSkeletonAnimation == null)
        {
            cachedSkeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (cachedSpineRenderer == null && cachedSkeletonAnimation != null)
        {
            cachedSpineRenderer = cachedSkeletonAnimation.GetComponent<MeshRenderer>();
        }

        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void EnsureRuntimeCombatComponents()
    {
        CombatStats stats = GetComponent<CombatStats>();
        float resolvedMaxHealth = stats != null && stats.maxHealth > 0f ? stats.maxHealth : 100f;

        BattleResourceBank bank = GetComponent<BattleResourceBank>();
        if (bank == null)
        {
            bank = gameObject.AddComponent<BattleResourceBank>();
            bank.maxHealth = resolvedMaxHealth;
            bank.currentHealth = resolvedMaxHealth;
            bank.maxEnergy = 100f;
            bank.currentEnergy = 100f;
        }
        else
        {
            bank.SyncHealthFromCombatStats(refillCurrentHealth: true);
        }

        CombatHealth health = GetComponent<CombatHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<CombatHealth>();
        }

        health.stats = stats;
        health.resourceBank = bank;
        health.SyncHealthFromStats(refillCurrentHealth: true);

        if (GetComponent<PlayerSkillCooldownManager>() == null)
        {
            PlayerSkillCooldownManager cooldownManager = gameObject.AddComponent<PlayerSkillCooldownManager>();
            cooldownManager.resourceBank = bank;
        }

        if (GetComponent<CombatSkillCaster>() == null)
        {
            gameObject.AddComponent<CombatSkillCaster>();
        }

        if (GetComponent<RuneInventory>() == null)
        {
            gameObject.AddComponent<RuneInventory>();
        }

        if (GetComponent<RuneLibrary>() == null)
        {
            gameObject.AddComponent<RuneLibrary>();
        }

        if (GetComponent<RuneSkillPanel>() == null)
        {
            gameObject.AddComponent<RuneSkillPanel>();
        }
    }

    private void AutoBindSkills()
    {
        if (qSkill == null) qSkill = GetComponent<Player1Skill_Q_QuickShear>();
        if (wSkill == null) wSkill = GetComponent<Player1Skill_W_ThreadFlow>();
        if (eSkill == null) eSkill = GetComponent<Player1Skill_E_BrokenDash>();
        if (rSkill == null) rSkill = GetComponent<Player1Skill_R_NeedleShot>();
    }

    private SkeletonAnimation ResolveSkeletonAnimation()
    {
        if (cachedSkeletonAnimation == null)
        {
            cachedSkeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        return cachedSkeletonAnimation;
    }

    private string ResolveLocomotionAnimationName()
    {
        float moveMagnitude = ResolveMoveMagnitude();
        if (moveMagnitude >= runSpeedThreshold && !string.IsNullOrWhiteSpace(runAnimationName))
        {
            return runAnimationName;
        }

        if (moveMagnitude >= walkSpeedThreshold && !string.IsNullOrWhiteSpace(walkAnimationName))
        {
            return walkAnimationName;
        }

        return idleAnimationName;
    }

    private float ResolveMoveMagnitude()
    {
        if (cachedMovement != null && cachedMovement.rb != null)
        {
            return cachedMovement.rb.linearVelocity.magnitude;
        }

        if (cachedRigidbody != null)
        {
            return cachedRigidbody.linearVelocity.magnitude;
        }

        return 0f;
    }

    private void ApplyFacingFromMovement()
    {
        if (IsSkillAnimationLocked() || skillFacingLocked)
        {
            cachedFacingScaleX = lockedFacingScaleX;
            if (debugLog && lastFacingLockLogFrame != Time.frameCount)
            {
                Debug.Log("[Facing] skipped because skill animation locked", this);
                lastFacingLockLogFrame = Time.frameCount;
            }

            return;
        }

        float horizontalInput = ResolveHorizontalInput();
        if (Mathf.Abs(horizontalInput) < 0.0001f)
        {
            return;
        }

        cachedFacingScaleX = horizontalInput > 0f ? -1 : 1;

        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine != null && spine.Skeleton != null)
        {
            spine.Skeleton.ScaleX = cachedFacingScaleX;
        }
    }

    private float ResolveHorizontalInput()
    {
        if (Keyboard.current == null)
        {
            return 0f;
        }

        float horizontal = 0f;
        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
        {
            horizontal += 1f;
        }

        return horizontal;
    }

    private void ApplyDisplayFixes(bool force)
    {
        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine != null && spine.Skeleton != null)
        {
            spine.Skeleton.ScaleX = cachedFacingScaleX;
        }

        if (cachedSpineRenderer != null && (force || cachedSpineRenderer.sortingOrder != spineSortingOrder))
        {
            cachedSpineRenderer.sortingOrder = spineSortingOrder;
        }
    }

    private bool IsSkillAnimationLocked()
    {
        return skillAnimationLocked && Time.time < skillAnimationLockUntil;
    }

    private void UpdateSkillLockState()
    {
        if (!IsSkillAnimationLocked())
        {
            if (skillAnimationLocked || skillFacingLocked || skillMovementFrozen)
            {
                ClearSkillAnimationLock();
            }
        }
    }

    private void FreezeMovementDuringSkillLock()
    {
        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedMovement == null || skillMovementFrozen)
        {
            return;
        }

        frozenMoveSpeed = cachedMovement.moveSpeed;
        cachedMovement.moveSpeed = 0f;
        skillMovementFrozen = true;

        if (debugLog)
        {
            Debug.Log($"[Movement] skill locked, moveSpeed frozen from {frozenMoveSpeed:F2} to 0.", this);
        }
    }

    private void RestoreMovementAfterSkillLock()
    {
        if (!skillMovementFrozen)
        {
            return;
        }

        if (cachedMovement == null)
        {
            cachedMovement = GetComponent<PlayerMovement>();
        }

        if (cachedMovement != null && frozenMoveSpeed >= 0f)
        {
            cachedMovement.moveSpeed = frozenMoveSpeed;
        }

        if (debugLog)
        {
            Debug.Log($"[Movement] skill lock ended, moveSpeed restored to {frozenMoveSpeed:F2}.", this);
        }

        frozenMoveSpeed = -1f;
        skillMovementFrozen = false;
    }

    private void TryCastSkillFromInput(string keyLabel, Player01SkillBase skill)
    {
        if (debugSkillCooldownFlow)
        {
            Debug.Log($"[SkillCD] Player01 {keyLabel} pressed", this);
        }

        if (skill == null)
        {
            Debug.LogWarning($"[Player01SkillController] {keyLabel} pressed, but no skill component is bound.", this);
            return;
        }

        PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
        if (skillHud != null && skillHud.IsSkillOnCooldown(keyLabel))
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player01 {keyLabel} blocked by HUD cooldown", this);
            }
            return;
        }

        if (currentSkill != null && currentSkill != skill)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player01 {keyLabel} blocked by active skill {currentSkill.name}", this);
            }
            Debug.Log($"[Player01SkillController] {keyLabel} pressed, but {currentSkill.name} is still active.", this);
            return;
        }

        if (!skill.CanCastNow())
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player01 {keyLabel} blocked by runtime cooldown or MP", this);
            }
            Debug.Log($"[Player01SkillController] {keyLabel} pressed, but {skill.name} is on cooldown.", this);
            return;
        }

        Debug.Log($"[Player01SkillController] {keyLabel} pressed -> {skill.name}.", this);
        bool castSuccess = skill.Cast();
        if (debugSkillCooldownFlow)
        {
            Debug.Log($"[SkillCD] Player01 {keyLabel} cast result = {castSuccess}", this);
        }

        if (!castSuccess)
        {
            return;
        }

        if (skillHud != null)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player01 {keyLabel} start HUD cooldown", this);
            }

            skillHud.StartSkillCooldown(keyLabel, ResolveHudCooldown(keyLabel));
        }
    }

    private float ResolveHudCooldown(string keyLabel)
    {
        switch (keyLabel.Trim().ToUpperInvariant())
        {
            case "Q":
                return qCooldown;
            case "W":
                return wCooldown;
            case "E":
                return eCooldown;
            case "R":
                return rCooldown;
            default:
                return 0f;
        }
    }

    private void SyncHudCooldownsToResourceManager()
    {
        PlayerSkillCooldownManager cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        if (cooldownManager == null || cooldownManager.skillDatas == null || cooldownManager.skillDatas.Length < 4)
        {
            return;
        }

        SkillCostCDData qData = cooldownManager.skillDatas[0];
        qData.maxCooldown = qCooldown;
        cooldownManager.skillDatas[0] = qData;

        SkillCostCDData wData = cooldownManager.skillDatas[1];
        wData.maxCooldown = wCooldown;
        cooldownManager.skillDatas[1] = wData;

        SkillCostCDData eData = cooldownManager.skillDatas[2];
        eData.maxCooldown = eCooldown;
        cooldownManager.skillDatas[2] = eData;

        SkillCostCDData rData = cooldownManager.skillDatas[3];
        rData.maxCooldown = rCooldown;
        cooldownManager.skillDatas[3] = rData;
    }

    private System.Collections.IEnumerator LogTrackAfterOneFrame(SkeletonAnimation spine, string expectedAnimation)
    {
        yield return null;

        string currentTrackAnimation = GetCurrentTrackAnimationName(0);
        if (currentTrackAnimation == expectedAnimation)
        {
            Debug.Log($"[TEST] Next frame Track0 is still {currentTrackAnimation}.", this);
            yield break;
        }

        Debug.LogWarning($"[TEST] Next frame Track0 changed to {currentTrackAnimation}. Current locomotion={currentLocomotionAnimation}, activeSkill={(currentSkill != null ? currentSkill.GetType().Name : "none")}.", this);
    }

    public string GetCurrentTrackAnimationName(int trackIndex)
    {
        SkeletonAnimation spine = ResolveSkeletonAnimation();
        return GetCurrentTrackAnimationName(spine, trackIndex);
    }

    private string GetCurrentTrackAnimationName(SkeletonAnimation spine, int trackIndex)
    {
        if (spine == null || spine.AnimationState == null)
        {
            return "<null>";
        }

        TrackEntry current = spine.AnimationState.GetCurrent(trackIndex);
        return current != null && current.Animation != null ? current.Animation.Name : "<none>";
    }

    public string GetSkeletonAnimationDebugSummary()
    {
        return DescribeSkeletonAnimation(ResolveSkeletonAnimation());
    }

    public string GetCurrentLocomotionAnimationName()
    {
        return currentLocomotionAnimation ?? "<none>";
    }

    public string GetCurrentSkillName()
    {
        return currentSkill != null ? currentSkill.GetType().Name : "<none>";
    }

    public bool IsVeilBarrierActive()
    {
        return wSkill is Player1Skill_W_ThreadFlow w && w.IsDefending;
    }

    private string DescribeSkeletonAnimation(SkeletonAnimation spine)
    {
        if (spine == null)
        {
            return "<null spine>";
        }

        string hierarchyPath = GetHierarchyPath(spine.transform);
        string skeletonAssetName = spine.skeletonDataAsset != null ? spine.skeletonDataAsset.name : "<missing>";
        string animations = "<none>";
        if (spine.Skeleton != null && spine.Skeleton.Data != null && spine.Skeleton.Data.Animations != null)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (var animation in spine.Skeleton.Data.Animations)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(animation != null ? animation.Name : "<null>");
            }

            animations = builder.ToString();
        }

        return $"SkeletonAnimation='{spine.gameObject.name}', path='{hierarchyPath}', skeletonDataAsset='{skeletonAssetName}', animations=[{animations}]";
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private TrackEntry PlayAnimationEquivalent(SkeletonAnimation spine, string animationName, bool loop, bool clearTrackFirst, bool verboseLog, string source)
    {
        if (spine == null || spine.Skeleton == null || spine.AnimationState == null)
        {
            if (verboseLog)
            {
                Debug.LogWarning($"[{source}] Cannot play '{animationName}' because SkeletonAnimation is missing on {name}.", this);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(animationName))
        {
            return null;
        }

        if (spine.Skeleton.Data == null || spine.Skeleton.Data.FindAnimation(animationName) == null)
        {
            if (verboseLog || debugLog)
            {
                Debug.LogWarning($"[TryPlayLockedSkillAnimation] animation not found: {animationName}. Available animations: {DescribeSkeletonAnimation(spine)}", this);
            }

            return null;
        }

        spine.loop = loop;
        spine.AnimationName = animationName;

        if (verboseLog)
        {
            Debug.Log($"[{source}] Play '{animationName}' on {spine.name}, clearTrackFirst={clearTrackFirst}, loop={loop}.", this);
        }

        if (clearTrackFirst)
        {
            spine.AnimationState.SetEmptyAnimation(0, 0f);
            if (verboseLog)
            {
                Debug.Log($"[{source}] SetEmptyAnimation(0, 0f).", this);
            }
        }

        TrackEntry entry = spine.AnimationState.SetAnimation(0, animationName, loop);
        if (entry != null)
        {
            entry.MixDuration = 0f;
            entry.TrackTime = 0f;
            if (verboseLog)
            {
                Debug.Log($"[{source}] TrackEntry set: animation={entry.Animation?.Name}, track={entry.TrackIndex}, duration={entry.Animation?.Duration:F3}, mixDuration={entry.MixDuration:F3}, skeletonAnimation.loop={spine.loop}, skeletonAnimation.AnimationName={spine.AnimationName}.", this);
            }
        }
        else if (verboseLog)
        {
            Debug.LogWarning($"[{source}] SetAnimation returned null for '{animationName}'.", this);
        }

        currentLocomotionAnimation = animationName;
        return entry;
    }
}
