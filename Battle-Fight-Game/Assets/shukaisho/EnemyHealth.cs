using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    public int hp = 3;

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
            Destroy(gameObject);
        }
    }
}
