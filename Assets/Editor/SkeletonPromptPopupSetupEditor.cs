using System.IO;
using Castlevania2D.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Imports the skeleton prompt image and wires a ground-line popup above SkeletTexture.
/// </summary>
public static class SkeletonPromptPopupSetupEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Setup Skeleton Prompt Popup";
    private const string SourcePath =
        @"C:\Users\Bensh\OneDrive\Рабочий стол\Персонаж\Интерфейс\1785324947_6a69e5937742c-Photoroom.png";
    private const string AssetPath = "Assets/Art/Sprites/UI/SkeletonPrompt.png";
    private const string ZoneObjectName = "SkeletonPromptZone";
    private const string PopupChildName = "PromptPopup";
    private const string SkeletonObjectName = "SkeletTexture";

    private static readonly Vector2 LineCenter = new Vector2(19.7f, -37.2f);
    private static readonly Vector3 PopupLocalPosition = new Vector3(0f, 2.35f, 0f);
    private static readonly Vector3 PopupScale = new Vector3(0.15f, 0.15f, 1f);

    [MenuItem(MenuPath)]
    public static void SetupSkeletonPromptPopup()
    {
        if (!File.Exists(SourcePath) && !File.Exists(AssetPath))
        {
            Debug.LogError($"SkeletonPromptPopupSetupEditor: source not found at {SourcePath}");
            return;
        }

        if (File.Exists(SourcePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
            File.Copy(SourcePath, AssetPath, overwrite: true);
        }

        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        ConfigureSpriteImport(AssetPath);

        Sprite promptSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        if (promptSprite == null)
        {
            Debug.LogError("SkeletonPromptPopupSetupEditor: failed to load prompt sprite after import.");
            return;
        }

        GameObject skeleton = GameObject.Find(SkeletonObjectName);
        Vector3 zonePosition = skeleton != null ? skeleton.transform.position : new Vector3(LineCenter.x, LineCenter.y, -1f);
        zonePosition.z = -1f;

        GameObject zone = GameObject.Find(ZoneObjectName);
        if (zone == null)
        {
            zone = new GameObject(ZoneObjectName);
            Undo.RegisterCreatedObjectUndo(zone, "Create Skeleton Prompt Zone");
        }

        zone.transform.position = zonePosition;
        zone.transform.rotation = Quaternion.identity;
        zone.transform.localScale = Vector3.one;

        SkeletonPromptPopup2D popup = zone.GetComponent<SkeletonPromptPopup2D>();
        if (popup == null)
        {
            popup = Undo.AddComponent<SkeletonPromptPopup2D>(zone);
        }

        Transform popupTransform = zone.transform.Find(PopupChildName);
        GameObject popupObject;
        if (popupTransform == null)
        {
            popupObject = new GameObject(PopupChildName);
            Undo.RegisterCreatedObjectUndo(popupObject, "Create Skeleton Prompt Popup");
            popupObject.transform.SetParent(zone.transform, false);
        }
        else
        {
            popupObject = popupTransform.gameObject;
        }

        popupObject.transform.localPosition = PopupLocalPosition;
        popupObject.transform.localRotation = Quaternion.identity;
        popupObject.transform.localScale = Vector3.zero;

        SpriteRenderer renderer = popupObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = Undo.AddComponent<SpriteRenderer>(popupObject);
        }

        renderer.sprite = promptSprite;
        renderer.sortingOrder = 5;
        renderer.enabled = false;
        renderer.color = Color.white;

        SerializedObject so = new SerializedObject(popup);
        so.FindProperty("lineCenter").vector2Value = LineCenter;
        so.FindProperty("lineHalfWidth").floatValue = 1.4f;
        so.FindProperty("lineHalfHeight").floatValue = 0.6f;
        so.FindProperty("promptSprite").objectReferenceValue = promptSprite;
        so.FindProperty("popupRenderer").objectReferenceValue = renderer;
        so.FindProperty("popupLocalPosition").vector3Value = PopupLocalPosition;
        so.FindProperty("popupScale").vector3Value = PopupScale;
        so.FindProperty("popDuration").floatValue = 0.18f;
        so.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
        so.ApplyModifiedPropertiesWithoutUndo();

        BoxCollider2D legacyTrigger = zone.GetComponent<BoxCollider2D>();
        if (legacyTrigger != null)
        {
            Undo.DestroyObjectImmediate(legacyTrigger);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = zone;

        Debug.Log($"SkeletonPromptPopupSetupEditor: prompt wired at line {LineCenter}, sprite {AssetPath}");
    }

    private static void ConfigureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePivot = new Vector2(0.5f, 0f);
        importer.SaveAndReimport();
    }
}
