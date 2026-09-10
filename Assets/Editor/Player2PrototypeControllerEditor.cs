using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Player2PrototypeController), true)]
[CanEditMultipleObjects]
public class Player2PrototypeControllerEditor : Editor
{
    private SerializedProperty qSkillProp;
    private SerializedProperty wSkillProp;
    private SerializedProperty eSkillProp;
    private SerializedProperty rSkillProp;

    private SerializedProperty dashDistanceProp;
    private SerializedProperty dashDurationProp;
    private SerializedProperty lockCharacterRotationProp;
    private SerializedProperty visualFloatHeightProp;
    private SerializedProperty debugVisualHeightProp;

    private SerializedProperty currentDivineMarkProp;

    private SerializedProperty sharedSkillEffectPrefabProp;
    private SerializedProperty rRenderCameraProp;
    private SerializedProperty rSwarmEnemyLayerProp;
    private SerializedProperty useRawPrefabRotationForSkillEffectsProp;

    private void OnEnable()
    {
        qSkillProp = serializedObject.FindProperty("qSkill");
        wSkillProp = serializedObject.FindProperty("wSkill");
        eSkillProp = serializedObject.FindProperty("eSkill");
        rSkillProp = serializedObject.FindProperty("rSkill");

        dashDistanceProp = serializedObject.FindProperty("dashDistance");
        dashDurationProp = serializedObject.FindProperty("dashDuration");
        lockCharacterRotationProp = serializedObject.FindProperty("lockCharacterRotation");
        visualFloatHeightProp = serializedObject.FindProperty("visualFloatHeight");
        debugVisualHeightProp = serializedObject.FindProperty("debugVisualHeight");

        currentDivineMarkProp = serializedObject.FindProperty("currentSwordEnergy");

        sharedSkillEffectPrefabProp = serializedObject.FindProperty("sharedSkillEffectPrefab");
        rRenderCameraProp = serializedObject.FindProperty("rRenderCamera");
        rSwarmEnemyLayerProp = serializedObject.FindProperty("rSwarmEnemyLayer");
        useRawPrefabRotationForSkillEffectsProp = serializedObject.FindProperty("useRawPrefabRotationForSkillEffects");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawMovementSection();
        DrawSkillSlotsSection();
        DrawDivineMarkSection();
        DrawSharedRuntimeSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMovementSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        DrawIfNotNull(dashDistanceProp);
        DrawIfNotNull(dashDurationProp);
        DrawIfNotNull(lockCharacterRotationProp);
        if (visualFloatHeightProp != null)
        {
            EditorGUILayout.PropertyField(
                visualFloatHeightProp,
                new GUIContent(
                    "Visual Float Height",
                    "Extra visual offset. Default 0 because Player02 Spine prefab base position already includes correct floating height."));
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawSkillSlotsSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Skill Slots", EditorStyles.boldLabel);
        DrawIfNotNull(qSkillProp);
        DrawIfNotNull(wSkillProp);
        DrawIfNotNull(eSkillProp);
        DrawIfNotNull(rSkillProp);
        EditorGUILayout.EndVertical();
    }

    private void DrawDivineMarkSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Divine Mark", EditorStyles.boldLabel);

        if (currentDivineMarkProp != null)
        {
            EditorGUILayout.PropertyField(currentDivineMarkProp, new GUIContent("Current Divine Mark"));
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSharedRuntimeSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Shared Runtime / Common", EditorStyles.boldLabel);

        DrawIfNotNull(sharedSkillEffectPrefabProp);
        DrawIfNotNull(rRenderCameraProp);
        DrawIfNotNull(rSwarmEnemyLayerProp);
        DrawIfNotNull(useRawPrefabRotationForSkillEffectsProp);
        DrawIfNotNull(debugVisualHeightProp);

        EditorGUILayout.EndVertical();
    }

    private static void DrawIfNotNull(SerializedProperty property)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property);
        }
    }
}
