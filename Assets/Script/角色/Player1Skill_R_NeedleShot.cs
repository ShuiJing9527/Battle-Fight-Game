using UnityEngine;

public class Player1Skill_R_NeedleShot : Player01SkillBase
{
    [Header("R")]
    [SerializeField] private GameObject needlePrefab;
    [SerializeField, Min(1)] private int needleCount = 3;
    [SerializeField] private float needleSpreadAngle = 10f;
    [SerializeField, Min(0f)] private float needleSpeed = 14f;
    [SerializeField, Min(0f)] private float needleDamage = 50f;
    [SerializeField, Range(0f, 1f)] private float healPercentOfDamage = 0.25f;
    [SerializeField] private LayerMask enemyLayer = ~0;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0.85f, 0.15f, 0f);

    private void Reset()
    {
        cooldown = 12f;
        duration = 0.45f;
        effectPower = 50f;
        animationName = "ATK01";
        debugLog = true;
        needleCount = 3;
        needleSpreadAngle = 10f;
        needleSpeed = 14f;
        needleDamage = 50f;
        healPercentOfDamage = 0.25f;
        enemyLayer = ~0;
        spawnOffset = new Vector3(0.85f, 0.15f, 0f);
    }

    protected override void OnCastStarted()
    {
        SpawnNeedles();

        if (debugLog)
        {
            Debug.Log($"[R - Needle Shot] fired. count={needleCount}, speed={needleSpeed:F2}", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "R - Needle Shot";
    }

    protected override int SkillIndex => 3;

    private void SpawnNeedles()
    {
        if (needlePrefab == null || Controller == null)
        {
            return;
        }

        Vector3 facing = Controller.GetFacingWorldDirection();
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.right;
        }

        Vector3 baseOffset = new Vector3(Mathf.Sign(facing.x) * spawnOffset.x, spawnOffset.y, spawnOffset.z);
        Vector3 spawnPosition = transform.position + baseOffset;
        int count = Mathf.Max(1, needleCount);
        float halfSpread = Mathf.Max(0f, needleSpreadAngle) * 0.5f;
        float step = count > 1 ? (halfSpread * 2f) / (count - 1) : 0f;
        float finalDamage = ResolveDamage(needleDamage);

        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? -halfSpread + step * i : 0f;
            Vector3 shotDirection = Quaternion.AngleAxis(angle, Vector3.forward) * facing;

            GameObject instance = Instantiate(needlePrefab, spawnPosition, Quaternion.identity);
            Player01NeedleProjectile projectile = instance.GetComponent<Player01NeedleProjectile>();
            if (projectile != null)
            {
                projectile.Launch(shotDirection, needleSpeed, finalDamage, gameObject, healPercentOfDamage, enemyLayer);
            }
            else if (instance.TryGetComponent<Rigidbody>(out Rigidbody projectileRb))
            {
                projectileRb.linearVelocity = shotDirection.normalized * needleSpeed;
            }
        }
    }

    private float ResolveDamage(float baseDamage)
    {
        float damage = Mathf.Max(0f, baseDamage);
        CombatStats stats = GetComponent<CombatStats>();
        if (stats != null)
        {
            damage += stats.specialAttack;
        }

        BattleResourceBank bank = GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            damage *= bank.SkillDamageMultiplier * bank.AttributeDamageMultiplier;
        }

        return damage;
    }
}
