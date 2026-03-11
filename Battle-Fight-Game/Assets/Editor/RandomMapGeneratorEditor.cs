using UnderTheStars.GenerationMap;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomMapGeneration), true)]
public class RandomMapGeneratorEditor : Editor
{
    RandomMapGeneration genenrator;

    private void Awake()
    {
        genenrator = (RandomMapGeneration)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("生成地图"))
        {
            genenrator.GenerationMap();
        }

        if (GUILayout.Button("重置地图"))
        {
            genenrator.ResetMapData();
        }
    }
}
