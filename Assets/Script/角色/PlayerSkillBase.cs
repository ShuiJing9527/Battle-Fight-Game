using UnityEngine;

public abstract class PlayerSkillBase : MonoBehaviour
{
    public Player2PrototypeController Owner { get; private set; }

    public virtual void Initialize(Player2PrototypeController owner)
    {
        Owner = owner;
    }

    public abstract bool Cast();

    public virtual void Cleanup()
    {
    }

    public virtual float ProcessIncomingDamageWithWGuard(float rawDamage, BattleDamage incomingDamage)
    {
        return Mathf.Max(0f, rawDamage);
    }
}
