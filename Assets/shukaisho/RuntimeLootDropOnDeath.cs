using UnityEngine;

public class RuntimeLootDropOnDeath : MonoBehaviour
{
    public float soulAmount = 15f;
    public float dropScatterRadius = 0.8f;

    private CombatHealth combatHealth;
    private EnemyHealth enemyHealth;
    private bool dropped;

    private void OnEnable()
    {
        combatHealth = GetComponent<CombatHealth>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (combatHealth != null)
        {
            combatHealth.Died += DropLoot;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += DropLoot;
        }
    }

    private void OnDisable()
    {
        if (combatHealth != null)
        {
            combatHealth.Died -= DropLoot;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died -= DropLoot;
        }
    }

    private void DropLoot(GameObject killer)
    {
        if (dropped)
        {
            return;
        }

        dropped = true;
        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;

        int soulCount = rank == MonsterRank.Boss ? 4 : (rank == MonsterRank.Elite ? 2 : 1);
        for (int i = 0; i < soulCount; i++)
        {
            CreateSoul(RandomSoulType(), soulAmount, transform.position + RandomOffset());
        }

        int runeCount = rank == MonsterRank.Boss ? 2 : (rank == MonsterRank.Elite ? 1 : 0);
        for (int i = 0; i < runeCount; i++)
        {
            CreateRune(transform.position + RandomOffset());
        }
    }

    private Vector3 RandomOffset()
    {
        Vector2 offset = Random.insideUnitCircle * dropScatterRadius;
        return new Vector3(offset.x, 0.15f, offset.y);
    }

    private static SoulType RandomSoulType()
    {
        int roll = Random.Range(0, 4);
        return roll switch
        {
            0 => SoulType.Life,
            1 => SoulType.Energy,
            2 => SoulType.Growth,
            _ => SoulType.Function
        };
    }

    private static void CreateSoul(SoulType type, float amount, Vector3 position)
    {
        GameObject soulObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soulObject.name = $"{type} Soul";
        soulObject.transform.position = position;
        soulObject.transform.localScale = Vector3.one * 0.35f;

        Collider collider = soulObject.GetComponent<Collider>();
        collider.isTrigger = true;

        Rigidbody rb = soulObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        SoulPickup pickup = soulObject.AddComponent<SoulPickup>();
        pickup.soulType = type;
        pickup.amount = amount;

        Renderer renderer = soulObject.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        renderer.material.color = SoulColor(type);
    }

    private void CreateRune(Vector3 position)
    {
        RuneLibrary library = FindObjectOfType<RuneLibrary>();
        RuneDefinition rune = library != null ? library.GetRandomRune() : RuneDefinition.CreateTableRune(RuneMechanic.Combo);
        if (rune == null)
        {
            return;
        }

        GameObject runeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runeObject.name = $"Rune - {rune.runeName}";
        runeObject.transform.position = position + Vector3.up * 0.2f;
        runeObject.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);

        Collider collider = runeObject.GetComponent<Collider>();
        collider.isTrigger = true;

        Rigidbody rb = runeObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        RunePickup pickup = runeObject.AddComponent<RunePickup>();
        pickup.rune = rune;

        Renderer renderer = runeObject.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        renderer.material.color = new Color(0.72f, 0.35f, 1f, 1f);
    }

    private static Color SoulColor(SoulType type)
    {
        return type switch
        {
            SoulType.Life => new Color(0.1f, 0.95f, 0.35f, 1f),
            SoulType.Energy => new Color(0.15f, 0.45f, 1f, 1f),
            SoulType.Growth => new Color(1f, 0.2f, 0.15f, 1f),
            SoulType.Function => new Color(1f, 0.82f, 0.12f, 1f),
            _ => Color.white
        };
    }
}
