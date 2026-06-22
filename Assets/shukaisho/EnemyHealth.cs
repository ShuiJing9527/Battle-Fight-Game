using UnityEngine;
using System;

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

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
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

        hp -= damage;
        Damaged?.Invoke(damage, attacker);
        TriggerAnimation(hitTrigger);

        if (hp <= 0)
        {
            dead = true;
            Died?.Invoke(attacker);
            TriggerAnimation(deathTrigger);
            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelayAfterDeath);
            }
        }
    }

    private void TriggerAnimation(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.SetTrigger(triggerName);
    }
}
