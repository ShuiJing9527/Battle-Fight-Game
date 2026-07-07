using UnderTheStars.GenerationMap;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomMapGeneration), true)]
public class RandomMapGenerationEditor : Editor
{
    private RandomMapGeneration generator;

    private void Awake()
    {
        generator = (RandomMapGeneration)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        if (target == null)
        {
            return;
        }

        if (GUILayout.Button("生成地图"))
        {
            generator.GenerateMap();
        }

        if (GUILayout.Button("重置地图"))
        {
            generator.ResetMapData();
        }
    }
}
