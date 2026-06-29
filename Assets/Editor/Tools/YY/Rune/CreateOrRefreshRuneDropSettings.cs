using UnityEditor;
using UnityEngine;

public static class CreateOrRefreshRuneDropSettings
{
    private const string SettingsPath = "Assets/Settings/RuneDropSettings.asset";
    private const string RuneDropPrefabFolder = "Assets/Prefabs/RuneDrops";

    [MenuItem("Tools/Battle-Fight-Game/Rune/Create Or Refresh Rune Drop Settings")]
    public static void CreateOrRefresh()
    {
        RuneDropSettings settings = AssetDatabase.LoadAssetAtPath<RuneDropSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RuneDropSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { RuneDropPrefabFolder });
        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty prefabArray = serializedSettings.FindProperty("runeDropPrefabs");
        prefabArray.arraySize = prefabGuids.Length;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            prefabArray.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
        Debug.Log($"[RuneDropSettings] Refreshed {prefabGuids.Length} rune drop prefab(s) from {RuneDropPrefabFolder}.", settings);
    }
}
