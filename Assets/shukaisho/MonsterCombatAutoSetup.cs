using UnityEngine;

public static class MonsterCombatAutoSetup
{
    public static void Configure(GameObject monster, MonsterSpecies? forcedSpecies = null, MonsterRank? forcedRank = null)
    {
        if (monster == null)
        {
            return;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            identity = monster.AddComponent<MonsterIdentity>();
        }

        identity.species = forcedSpecies ?? identity.species;
        if (!forcedSpecies.HasValue && identity.species == MonsterSpecies.BlueSlime)
        {
            identity.species = ResolveSpecies(monster.name);
        }

        identity.rank = forcedRank ?? identity.rank;
        if (!forcedRank.HasValue && identity.rank == MonsterRank.Normal)
        {
            identity.rank = ResolveRank(identity.species);
        }
        identity.attackStyle = ResolveAttackStyle(identity.species, identity.rank);

        ApplyScale(monster.transform, identity);
        ApplyStats(monster, identity);
        EnsureRuntimeComponents(monster);
        ApplyVisualMarker(monster, identity);
    }

    private static MonsterSpecies ResolveSpecies(string monsterName)
    {
        string lower = monsterName.ToLowerInvariant();
        if (lower.Contains("green")) return MonsterSpecies.GreenSlime;
        if (lower.Contains("lava")) return MonsterSpecies.LavaSlime;
        if (lower.Contains("poison")) return MonsterSpecies.PoisonSlime;
        if (lower.Contains("rainbow")) return MonsterSpecies.RainbowSlime;
        return MonsterSpecies.BlueSlime;
    }

    private static MonsterRank ResolveRank(MonsterSpecies species)
    {
        return species == MonsterSpecies.RainbowSlime ? MonsterRank.Boss : MonsterRank.Normal;
    }

    private static MonsterAttackStyle ResolveAttackStyle(MonsterSpecies species, MonsterRank rank)
    {
        if (rank == MonsterRank.Boss || species == MonsterSpecies.RainbowSlime)
        {
            return MonsterAttackStyle.ElementalBoss;
        }

        return species == MonsterSpecies.Ranged || species == MonsterSpecies.Flying
            ? MonsterAttackStyle.Ranged
            : MonsterAttackStyle.Melee;
    }

    private static void ApplyStats(GameObject monster, MonsterIdentity identity)
    {
        CombatStats stats = monster.GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = monster.AddComponent<CombatStats>();
        }

        float maxHealth = 35f;
        float attack = 7f;
        float defense = 0f;
        float speed = 2.5f;
        float range = 1.35f;
        float hitRange = 1.6f;
        float cooldown = 1.1f;

        switch (identity.species)
        {
            case MonsterSpecies.GreenSlime:
                maxHealth = 42f; attack = 6f; speed = 2.8f; break;
            case MonsterSpecies.LavaSlime:
                maxHealth = 48f; attack = 11f; defense = 1f; speed = 2.2f; break;
            case MonsterSpecies.PoisonSlime:
                maxHealth = 44f; attack = 8f; speed = 2.4f; cooldown = 0.95f; break;
            case MonsterSpecies.RainbowSlime:
                maxHealth = 220f; attack = 18f; defense = 2f; speed = 2f; range = 8f; hitRange = 8f; cooldown = 2.2f; break;
            case MonsterSpecies.Flying:
                maxHealth = 28f; attack = 7f; speed = 3.6f; range = 5.5f; hitRange = 5.5f; break;
            case MonsterSpecies.Ranged:
                maxHealth = 32f; attack = 9f; speed = 2f; range = 7f; hitRange = 7f; cooldown = 1.7f; break;
            case MonsterSpecies.Tank:
                maxHealth = 95f; attack = 10f; defense = 4f; speed = 1.25f; cooldown = 1.4f; break;
            case MonsterSpecies.Assassin:
                maxHealth = 26f; attack = 14f; speed = 4.4f; cooldown = 0.75f; break;
        }

        if (identity.rank == MonsterRank.Elite)
        {
            maxHealth *= 1.8f;
            attack *= 1.35f;
            defense += 1f;
        }
        else if (identity.rank == MonsterRank.Boss)
        {
            maxHealth *= 1.2f;
            attack *= 1.25f;
        }

        stats.maxHealth = maxHealth;
        stats.physicalAttack = attack;
        stats.specialAttack = attack;
        stats.physicalDefense = defense;
        stats.specialDefense = defense;
        stats.speed = speed;

        CombatHealth health = monster.GetComponent<CombatHealth>();
        if (health == null)
        {
            health = monster.AddComponent<CombatHealth>();
        }

        health.stats = stats;
        if (health.currentHealth <= 3f || health.currentHealth > stats.maxHealth)
        {
            health.currentHealth = stats.maxHealth;
        }

        EnemyController controller = monster.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.ConfigureRuntime(speed, 0.8f, range, hitRange, cooldown, attack, identity.attackStyle);
        }
    }

    private static void EnsureRuntimeComponents(GameObject monster)
    {
        if (monster.GetComponent<WorldHealthBar>() == null)
        {
            monster.AddComponent<WorldHealthBar>();
        }
    }

    private static void ApplyScale(Transform transform, MonsterIdentity identity)
    {
        if (identity.rank == MonsterRank.Boss)
        {
            transform.localScale = Vector3.one * 2.4f;
        }
        else if (identity.rank == MonsterRank.Elite)
        {
            transform.localScale = Vector3.one * 1.45f;
        }
    }

    private static void ApplyVisualMarker(GameObject monster, MonsterIdentity identity)
    {
        Transform oldMarker = monster.transform.Find("RankMarker");
        if (oldMarker != null)
        {
            Object.Destroy(oldMarker.gameObject);
        }

        if (identity.rank == MonsterRank.Normal)
        {
            return;
        }

        Color markerColor = identity.rank == MonsterRank.Boss
            ? new Color(1f, 0.7f, 0.15f, 0.85f)
            : new Color(0.85f, 0.25f, 1f, 0.75f);

        Light light = monster.GetComponent<Light>();
        if (light == null)
        {
            light = monster.AddComponent<Light>();
        }

        light.type = LightType.Point;
        light.range = identity.rank == MonsterRank.Boss ? 6f : 3f;
        light.intensity = identity.rank == MonsterRank.Boss ? 1.4f : 0.7f;
        light.color = markerColor;
    }
}
