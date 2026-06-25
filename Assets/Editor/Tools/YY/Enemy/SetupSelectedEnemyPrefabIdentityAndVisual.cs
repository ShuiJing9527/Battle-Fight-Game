using UnityEditor;
using UnityEngine;

public static class SetupSelectedEnemyPrefabIdentityAndVisual
{
    [MenuItem("Tools/YY/Enemy/Setup Selected Enemy Prefab Identity And Visual")]
    public static void SetupSelectedPrefabs()
    {
        GameObject[] selectedPrefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        if (selectedPrefabs == null || selectedPrefabs.Length == 0)
        {
            EditorUtility.DisplayDialog("Enemy Prefab Setup", "Please select one or more enemy prefab assets in the Project window.", "OK");
            return;
        }

        int updatedCount = 0;
        for (int i = 0; i < selectedPrefabs.Length; i++)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedPrefabs[i]);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab"))
            {
                continue;
            }

            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Prefabs/Enemy/"))
            {
                continue;
            }

            if (SetupPrefabAtPath(assetPath))
            {
                updatedCount++;
            }
        }

        if (updatedCount == 0)
        {
            EditorUtility.DisplayDialog("Enemy Prefab Setup", "No enemy prefab assets were updated.", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Enemy Prefab Setup", $"Updated {updatedCount} enemy prefab asset(s).", "OK");
    }

    private static bool SetupPrefabAtPath(string assetPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
        if (prefabRoot == null)
        {
            return false;
        }

        try
        {
            bool changed = false;

            MonsterIdentity identity = prefabRoot.GetComponent<MonsterIdentity>();
            bool createdIdentity = false;
            if (identity == null)
            {
                identity = prefabRoot.AddComponent<MonsterIdentity>();
                createdIdentity = true;
                changed = true;
            }

            if (createdIdentity)
            {
                identity.species = ResolveSpecies(prefabRoot.name);
                identity.rank = ResolveRank(prefabRoot.name);
            }

            identity.attackStyle = ResolveAttackStyle(identity.species, identity.rank);

            MonsterRankVisual rankVisual = prefabRoot.GetComponent<MonsterRankVisual>();
            bool createdRankVisual = false;
            if (rankVisual == null)
            {
                rankVisual = prefabRoot.AddComponent<MonsterRankVisual>();
                createdRankVisual = true;
                changed = true;
            }

            if (rankVisual.visualRoot == null)
            {
                rankVisual.visualRoot = prefabRoot.transform;
                changed = true;
            }

            if (rankVisual.effectRoot == null)
            {
                rankVisual.effectRoot = prefabRoot.transform;
                changed = true;
            }

            if (createdRankVisual)
            {
                rankVisual.normalScale = 1f;
                rankVisual.eliteScale = 1.45f;
                rankVisual.bossScale = 2.4f;
                rankVisual.applyScale = true;
                rankVisual.createFallbackLight = true;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            }

            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static MonsterSpecies ResolveSpecies(string prefabName)
    {
        string lower = prefabName.ToLowerInvariant();
        if (lower.Contains("green"))
        {
            return MonsterSpecies.GreenSlime;
        }

        if (lower.Contains("lava"))
        {
            return MonsterSpecies.LavaSlime;
        }

        if (lower.Contains("poison"))
        {
            return MonsterSpecies.PoisonSlime;
        }

        if (lower.Contains("rainbow"))
        {
            return MonsterSpecies.RainbowSlime;
        }

        return MonsterSpecies.BlueSlime;
    }

    private static MonsterRank ResolveRank(string prefabName)
    {
        string lower = prefabName.ToLowerInvariant();
        return lower.Contains("rainbow") ? MonsterRank.Boss : MonsterRank.Normal;
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
}
