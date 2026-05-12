using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    public int hp = 3;
    public bool destroyOnDeath = true;

    public event Action<GameObject> Died;

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        hp -= damage;

        if (hp <= 0)
        {
            Died?.Invoke(attacker);
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
