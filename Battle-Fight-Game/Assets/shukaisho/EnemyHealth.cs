using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int hp = 3;

    public void TakeDamage(int damage)
    {
        hp -= damage;

        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }
}