using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Imports SkeletTexture.png and places it in the open Prototype scene.
/// </summary>
public static class SkeletTextureSetupEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Place Skelet Texture in Scene";
    private const string AssetPath = "Assets/Art/Sprites/Characters/Enemies/Skeleton/SkeletTexture.png";
    private const string SourcePath = @"C:\Users\Bensh\Downloads\SkeletTexture.png";
    private const string SceneObjectName = "SkeletTexture";

    private static readonly Vector3 DefaultPosition = new Vector3(19.7f, -37.2f, -1f);
    private static readonly Vector3 DefaultScale = new Vector3(0.14f, 0.14f, 1f);

    [MenuItem(MenuPath)]
    public static void PlaceSkeletTexture()
    {
        if (!System.IO.File.Exists(SourcePath) && !System.IO.File.Exists(AssetPath))
        {
            Debug.LogError($"SkeletTextureSetupEditor: source not found at {SourcePath}");
            return;
        }

        if (System.IO.File.Exists(SourcePath))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath)!);
            System.IO.File.Copy(SourcePath, AssetPath, overwrite: true);
        }

        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsToUnits = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        if (sprite == null)
        {
            Debug.LogError("SkeletTextureSetupEditor: failed to load sprite after import.");
            return;
        }

        GameObject existing = GameObject.Find(SceneObjectName);
        GameObject root;
        if (existing != null)
        {
            root = existing;
        }
        else
        {
            root = new GameObject(SceneObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Place Skelet Texture");
        }

        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = Undo.AddComponent<SpriteRenderer>(root);
        }

        renderer.sprite = sprite;
        renderer.sortingOrder = 1;

        root.transform.position = DefaultPosition;
        root.transform.localScale = DefaultScale;
        root.transform.rotation = Quaternion.identity;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log($"Placed {SceneObjectName} at {DefaultPosition} using {AssetPath}");
    }
}
