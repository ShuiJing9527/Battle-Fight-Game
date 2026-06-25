using UnityEngine;

public class RuntimeLootDropOnDeath : MonoBehaviour
{
    [Header("Soul Drop")]
    [SerializeField] private SoulPickup soulPrefab;
    [SerializeField, Min(0f)] private float dropYOffset = 1f;
    public float soulAmount = 15f;
    public float dropScatterRadius = 0.8f;

    [Header("Rune Drop")]
    [SerializeField] private RunePickup runePickupPrefab;
    [SerializeField, Min(0f)] private float runeDropYOffset = 0.25f;

    private CombatHealth combatHealth;
    private EnemyHealth enemyHealth;
    private RuneDropManager runeDropManager;
    private bool dropped;
    private bool triedMissingHealthLog;
    private bool deathEventsBound;
    private bool triedMissingRuneDropManager;
    private bool warnedMissingSoulPrefab;
    private bool warnedMissingRunePickupPrefab;

    private void OnEnable()
    {
        dropped = false;
        triedMissingHealthLog = false;
        deathEventsBound = false;
        warnedMissingSoulPrefab = false;
        warnedMissingRunePickupPrefab = false;
        TryBindDeathEvents(true);
    }

    private void Start()
    {
        TryBindDeathEvents(true);
    }

    private void Update()
    {
        if (!deathEventsBound)
        {
            TryBindDeathEvents(false);
        }
    }

    private void OnDisable()
    {
        if (deathEventsBound && combatHealth != null)
        {
            combatHealth.Died -= DropLoot;
        }

        if (deathEventsBound && enemyHealth != null)
        {
            enemyHealth.Died -= DropLoot;
        }

        deathEventsBound = false;
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
            CreateSoul(RandomSoulType(), soulAmount, transform.position + Vector3.up * dropYOffset + RandomOffset());
        }

        int runeCount = rank == MonsterRank.Boss ? 2 : (rank == MonsterRank.Elite ? 1 : 0);
        for (int i = 0; i < runeCount; i++)
        {
            CreateRune(transform.position + Vector3.up * runeDropYOffset);
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

    private void CreateSoul(SoulType type, float amount, Vector3 position)
    {
        if (soulPrefab != null)
        {
            SoulPickup pickup = Instantiate(soulPrefab, position, Quaternion.identity);
            pickup.soulType = type;
            pickup.amount = amount;
            pickup.destroyAfterPickup = true;
            pickup.gameObject.SetActive(true);
            pickup.Configure(type, amount);
            EnsureDebugVisible(pickup.gameObject);
            return;
        }

        WarnMissingSoulPrefabOnce();
        GameObject soulObject = new GameObject($"{type} Soul");
        soulObject.transform.position = position;
        soulObject.transform.localScale = Vector3.one * 0.35f;

        SphereCollider collider = soulObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        Rigidbody rb = soulObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SoulPickup fallbackPickup = soulObject.AddComponent<SoulPickup>();
        fallbackPickup.Configure(type, amount);
        soulObject.SetActive(true);
    }

    private void TryBindDeathEvents(bool logMissing)
    {
        if (deathEventsBound)
        {
            return;
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (combatHealth != null)
        {
            combatHealth.Died += DropLoot;
            deathEventsBound = true;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += DropLoot;
            deathEventsBound = true;
        }

        if (deathEventsBound)
        {
            return;
        }

        if (logMissing && !triedMissingHealthLog)
        {
            triedMissingHealthLog = true;
            Debug.LogWarning($"[RuntimeLootDropOnDeath] no health component found enemy={name}", this);
        }
    }

    private void EnsureDebugVisible(GameObject soulRoot)
    {
        if (soulRoot == null)
        {
            return;
        }

        Renderer[] renderers = soulRoot.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                hasVisibleRenderer = true;
                break;
            }
        }

        if (hasVisibleRenderer)
        {
            return;
        }

        GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugSphere.name = "SoulDropDebugVisible";
        debugSphere.transform.SetParent(soulRoot.transform, false);
        debugSphere.transform.localPosition = Vector3.zero;
        debugSphere.transform.localScale = Vector3.one * 0.25f;

        Collider collider = debugSphere.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        Renderer renderer = debugSphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
            material.color = new Color(1f, 0.95f, 0.7f, 1f);
            renderer.material = material;
        }
    }

    private void CreateRune(Vector3 position)
    {
        if (TrySpawnRuneFromManager(position))
        {
            return;
        }

        RuneLibrary library = FindObjectOfType<RuneLibrary>();
        RuneDefinition rune = library != null ? library.GetRandomRune() : RuneDefinition.CreateTableRune(RuneMechanic.Combo);
        if (rune == null)
        {
            return;
        }

        if (runePickupPrefab != null)
        {
            RunePickup pickup = Instantiate(runePickupPrefab, position, Quaternion.identity);
            pickup.rune = rune;
            pickup.destroyAfterPickup = true;
            pickup.gameObject.SetActive(true);
            return;
        }

        WarnMissingRunePickupPrefabOnce();
        GameObject runeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runeObject.name = $"Rune - {rune.runeName}";
        runeObject.transform.position = position;
        runeObject.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);

        Collider collider = runeObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Rigidbody rb = runeObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = runeObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        RunePickup fallbackPickup = runeObject.GetComponent<RunePickup>();
        if (fallbackPickup == null)
        {
            fallbackPickup = runeObject.AddComponent<RunePickup>();
        }
        fallbackPickup.rune = rune;
        fallbackPickup.destroyAfterPickup = true;
    }

    private bool TrySpawnRuneFromManager(Vector3 position)
    {
        RuneDropManager manager = ResolveRuneDropManager();
        if (manager == null)
        {
            return false;
        }

        RunePickup pickup = manager.SpawnRandomRune(position);
        return pickup != null;
    }

    private RuneDropManager ResolveRuneDropManager()
    {
        if (runeDropManager != null)
        {
            return runeDropManager;
        }

        runeDropManager = Object.FindFirstObjectByType<RuneDropManager>();
        if (runeDropManager == null && !triedMissingRuneDropManager)
        {
            triedMissingRuneDropManager = true;
            Debug.LogWarning($"[RuntimeLootDropOnDeath] RuneDropManager not found in scene. Falling back to local runePrefab on {name}.", this);
        }

        return runeDropManager;
    }

    private void WarnMissingSoulPrefabOnce()
    {
        if (warnedMissingSoulPrefab)
        {
            return;
        }

        warnedMissingSoulPrefab = true;
        Debug.LogWarning("[SoulDrop] soulPrefab missing, fallback simple soul created.", this);
    }

    private void WarnMissingRunePickupPrefabOnce()
    {
        if (warnedMissingRunePickupPrefab)
        {
            return;
        }

        warnedMissingRunePickupPrefab = true;
        Debug.LogWarning("[RuneDrop] runePickupPrefab missing, fallback simple rune created.", this);
    }

}
