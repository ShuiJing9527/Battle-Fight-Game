using UnityEngine;
using System;

/// <summary>
/// Legacy compatibility wrapper. New enemy health logic lives in CombatHealth.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    public int hp = 3;
    public bool destroyOnDeath = true;
    public Animator animator;
    public string hitTrigger = "Hit";
    public string deathTrigger = "Die";
    [Min(0f)] public float destroyDelayAfterDeath = 0.65f;

    public event Action<GameObject> Died;
    public event Action<int, GameObject> Damaged;

    private bool dead;
    private CombatHealth combatHealth;
    private bool eventsBound;
    private bool createdCombatHealth;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        EnsureCombatHealth();
        BindCombatHealthEvents();
        SyncFromCombatHealth();

        if (GetComponent<DissolveOnDeath>() == null)
        {
            gameObject.AddComponent<DissolveOnDeath>();
        }
    }

    private void OnEnable()
    {
        EnsureCombatHealth();
        BindCombatHealthEvents();
        SyncFromCombatHealth();
    }

    private void OnDisable()
    {
        UnbindCombatHealthEvents();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        if (dead)
        {
            return;
        }

        if (attacker != null && !BattleTargetUtility.IsPlayer(attacker))
        {
            return;
        }

        CombatHealth resolvedHealth = EnsureCombatHealth();
        if (resolvedHealth == null)
        {
            return;
        }

        resolvedHealth.TakeDamage(new BattleDamage(Mathf.Max(0, damage), BattleDamageType.Physical, attacker));
    }

    public CombatHealth EnsureCombatHealth()
    {
        if (combatHealth != null)
        {
            return combatHealth;
        }

        CombatStats stats = GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<CombatStats>();
            stats.maxHealth = Mathf.Max(1, hp);
        }

        BattleResourceBank resourceBank = GetComponent<BattleResourceBank>();
        if (resourceBank == null)
        {
            resourceBank = gameObject.AddComponent<BattleResourceBank>();
        }

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            gameObject.AddComponent<MonsterIdentity>();
        }

        combatHealth = GetComponent<CombatHealth>();
        if (combatHealth == null)
        {
            combatHealth = gameObject.AddComponent<CombatHealth>();
            createdCombatHealth = true;
        }

        combatHealth.stats = stats;
        combatHealth.resourceBank = resourceBank;
        combatHealth.animator = animator != null ? animator : combatHealth.animator;
        combatHealth.hitTrigger = hitTrigger;
        combatHealth.deathTrigger = deathTrigger;
        combatHealth.destroyDelayAfterDeath = destroyDelayAfterDeath;
        if (createdCombatHealth)
        {
            combatHealth.destroyOnDeath = destroyOnDeath;
        }

        combatHealth.SyncHealthFromStats(refillCurrentHealth: false);
        return combatHealth;
    }

    private void BindCombatHealthEvents()
    {
        if (eventsBound || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged += HandleCombatDamaged;
        combatHealth.Died += HandleCombatDied;
        eventsBound = true;
    }

    private void UnbindCombatHealthEvents()
    {
        if (!eventsBound || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged -= HandleCombatDamaged;
        combatHealth.Died -= HandleCombatDied;
        eventsBound = false;
    }

    private void HandleCombatDamaged(float amount, GameObject attacker)
    {
        SyncFromCombatHealth();
        Damaged?.Invoke(Mathf.RoundToInt(amount), attacker);
    }

    private void HandleCombatDied(GameObject attacker)
    {
        SyncFromCombatHealth();
        dead = true;
        Died?.Invoke(attacker);
    }

    private void SyncFromCombatHealth()
    {
        if (combatHealth == null)
        {
            return;
        }

        float currentHealth = combatHealth.resourceBank != null
            ? combatHealth.resourceBank.currentHealth
            : combatHealth.currentHealth;
        hp = Mathf.RoundToInt(Mathf.Max(0f, currentHealth));
        dead = combatHealth.IsDead || hp <= 0;
    }

    private void OnValidate()
    {
        hp = Mathf.Max(0, hp);
        destroyDelayAfterDeath = Mathf.Max(0f, destroyDelayAfterDeath);
    }
}
