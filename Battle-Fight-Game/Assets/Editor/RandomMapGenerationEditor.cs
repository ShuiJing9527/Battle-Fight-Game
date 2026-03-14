using UnderTheStars.GenerationMap;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomMapGeneration), true)]
public class RandomMapGenerationEditor : Editor
{
    RandomMapGeneration generator;

    private void Awake()
    {
        generator = (RandomMapGeneration)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

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
