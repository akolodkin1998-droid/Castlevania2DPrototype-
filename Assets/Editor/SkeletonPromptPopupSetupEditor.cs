using Castlevania2D.Level;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires the skeleton ground-line zone to the screen parchment hint. Does not save the scene.
/// </summary>
public static class SkeletonPromptPopupSetupEditor
{
    private const string MenuPath = "Tools/Castlevania 2D/Setup Skeleton Prompt Popup";
    private const string ZoneObjectName = "SkeletonPromptZone";
    private const string PopupChildName = "PromptPopup";
    private const string SkeletonObjectName = "SkeletTexture";
    private const string DefaultHintText =
        "Иногда нужно отбить предмет. Нажми W + ПКМ — так можно закрыться от камня сверху и забросить его в корзину.";

    private static readonly Vector2 LineCenter = new Vector2(19.7f, -37.2f);

    [MenuItem(MenuPath)]
    public static void SetupSkeletonPromptPopup()
    {
        GameObject skeleton = GameObject.Find(SkeletonObjectName);
        Vector3 zonePosition = skeleton != null
            ? skeleton.transform.position
            : new Vector3(LineCenter.x, LineCenter.y, -1f);
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
        if (popupTransform != null)
        {
            Undo.DestroyObjectImmediate(popupTransform.gameObject);
        }

        SerializedObject so = new SerializedObject(popup);
        so.FindProperty("lineCenter").vector2Value = LineCenter;
        so.FindProperty("lineHalfWidth").floatValue = 1.4f;
        so.FindProperty("lineHalfHeight").floatValue = 0.6f;
        so.FindProperty("hintText").stringValue = DefaultHintText;
        so.FindProperty("playerObjectName").stringValue = "Player_HeroKnight";
        so.ApplyModifiedPropertiesWithoutUndo();

        BoxCollider2D legacyTrigger = zone.GetComponent<BoxCollider2D>();
        if (legacyTrigger != null)
        {
            Undo.DestroyObjectImmediate(legacyTrigger);
        }

        Selection.activeGameObject = zone;
        Debug.Log($"SkeletonPromptPopupSetupEditor: parchment hint on zone {ZoneObjectName} at line {LineCenter}");
    }
}
