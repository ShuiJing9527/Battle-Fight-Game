using UnityEngine;

public class Player1Skill_W_ThreadFlow : Player01SkillBase
{
    [Header("W")]
    [SerializeField, Min(0f)] private float damageReduction = 0.4f;
    [SerializeField] private GameObject shieldPrefab;

    public bool IsDefending { get; private set; }

    private GameObject activeShieldInstance;

    private void Reset()
    {
        cooldown = 4.5f;
        duration = 0.75f;
        effectPower = 0.8f;
        animationName = "";
        debugLog = true;
        damageReduction = 0.4f;
    }

    public override bool LocksLocomotionAnimation()
    {
        return false;
    }

    protected override void OnCastStarted()
    {
        IsDefending = true;

        if (shieldPrefab != null)
        {
            activeShieldInstance = Instantiate(shieldPrefab, transform.position, transform.rotation, transform);
        }

        if (debugLog)
        {
            Debug.Log($"[W - 丝缕缠流] Defense state entered. damageReduction={damageReduction:F2}", this);
        }
    }

    protected override void OnCastFinished()
    {
        IsDefending = false;

        if (activeShieldInstance != null)
        {
            Destroy(activeShieldInstance);
            activeShieldInstance = null;
        }
    }

    protected override string GetSkillLabel()
    {
        return "W - 丝缕缠流";
    }
}
