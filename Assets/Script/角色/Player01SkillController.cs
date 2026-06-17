using UnityEngine;
using UnityEngine.InputSystem;
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
    [SerializeField, Min(0f)] private float runSpeedThreshold = 2.5f;

    [Header("Display Fix")]
    [SerializeField] private int spineSortingOrder = 20;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private SkeletonAnimation cachedSkeletonAnimation;
    private MeshRenderer cachedSpineRenderer;
    private PlayerMovement cachedMovement;
    private Rigidbody cachedRigidbody;
    private Player01SkillBase currentSkill;
    private string currentLocomotionAnimation;
    private int cachedFacingScaleX = 1;

    private void Reset()
    {
        CacheReferences();
        AutoBindSkills();
    }

    private void Awake()
    {
        CacheReferences();
        AutoBindSkills();
        InitializeSkills();
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
        ApplyFacingFromMovement();

        if (Keyboard.current == null)
        {
            if (currentSkill == null)
            {
                RestoreLocomotionAnimation(false);
            }

            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame && qSkill != null)
        {
            qSkill.Cast();
        }

        if (Keyboard.current.wKey.wasPressedThisFrame && wSkill != null)
        {
            wSkill.Cast();
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && eSkill != null)
        {
            eSkill.Cast();
        }

        if (Keyboard.current.rKey.wasPressedThisFrame && rSkill != null)
        {
            rSkill.Cast();
        }

        if (currentSkill == null)
        {
            RestoreLocomotionAnimation(false);
        }
    }

    private void LateUpdate()
    {
        ApplyDisplayFixes(false);
    }

    public void InitializeSkills()
    {
        if (qSkill != null) qSkill.Initialize(this);
        if (wSkill != null) wSkill.Initialize(this);
        if (eSkill != null) eSkill.Initialize(this);
        if (rSkill != null) rSkill.Initialize(this);
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
        RestoreLocomotionAnimation(true);
    }

    public bool TryPlaySkillAnimation(string animationName, bool loop)
    {
        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine == null || spine.Skeleton == null || spine.AnimationState == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        if (spine.Skeleton.Data == null || spine.Skeleton.Data.FindAnimation(animationName) == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Player01SkillController] Missing Spine animation '{animationName}' on {name}.", this);
            }

            return false;
        }

        spine.AnimationState.SetAnimation(0, animationName, loop);
        currentLocomotionAnimation = animationName;
        return true;
    }

    public void RestoreLocomotionAnimation(bool force = false)
    {
        if (currentSkill != null && !force)
        {
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

        TryPlaySkillAnimation(animation, true);
        currentLocomotionAnimation = animation;
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
        float horizontalSpeed = ResolveHorizontalSpeed();
        if (Mathf.Abs(horizontalSpeed) < 0.0001f)
        {
            return;
        }

        cachedFacingScaleX = horizontalSpeed > 0f ? -1 : 1;

        SkeletonAnimation spine = ResolveSkeletonAnimation();
        if (spine != null && spine.Skeleton != null)
        {
            spine.Skeleton.ScaleX = cachedFacingScaleX;
        }
    }

    private float ResolveHorizontalSpeed()
    {
        if (cachedMovement != null && cachedMovement.rb != null)
        {
            return cachedMovement.rb.linearVelocity.x;
        }

        if (cachedRigidbody != null)
        {
            return cachedRigidbody.linearVelocity.x;
        }

        return 0f;
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
}
