using Castlevania2D.Enemies;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FlowerBossTentacleAttack2D))]
public sealed class FlowerBossTentacleAttack2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("SPAWN PLACEMENT", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Wall Spawn X — левая сторона арены (~-11.4): меньше = в стену, больше = в арену.\n" +
            "Right Wall Spawn X — правая сторона (~13): больше = в стену, меньше = в арену.\n" +
            "Щупальца определяют сторону по world X (внутрь арены),\n" +
            "даже после свапа LeftWall↔RightWall.\n" +
            "RightWall (Ground_5) — левая сторона; LeftWall — правая.\n" +
            "Ground Spawn Y — низ щупальца на полу / под героем (~0.5).\n" +
            "Wall Spawn Height — высота на блоке стены (0..1).",
            MessageType.Info);

        DrawProperty("wallSpawnX");
        DrawProperty("rightWallSpawnX");
        DrawProperty("groundSpawnY");
        DrawProperty("wallSpawnHeightNormalized");

        EditorGUILayout.Space(8f);
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "wallSpawnX",
            "rightWallSpawnX",
            "groundSpawnY",
            "wallSpawnHeightNormalized");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Поле '{propertyName}' не найдено. Дождись компиляции скриптов (смотри Console).",
                MessageType.Warning);
        }
    }
}
