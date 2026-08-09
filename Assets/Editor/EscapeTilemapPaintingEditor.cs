#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Escapes Unity Tile Palette painting when com.unity.2d.tilemap throws
/// OverflowException in PaintableGrid.UpdateMouseGridPosition (Math.Sign(int.MinValue)),
/// which leaves Scene View GUI clips unbalanced and spam "GUI Error: pushing more GUIClips".
/// Scene data is usually fine; the package path runs while Grid Painting tools are active.
/// </summary>
internal static class EscapeTilemapPaintingEditor
{
    [MenuItem("Tools/Castlevania 2D/Escape Tilemap Painting (fix GUIClips spam)")]
    private static void Escape()
    {
        // Leave paint/erase/box/pick tools so PaintableSceneViewGrid stops mouse→cell conversion.
        // ViewModeTool is internal in UnityEditor; use the public Tool.View enum instead.
        Tools.current = Tool.View;

        foreach (SceneView view in SceneView.sceneViews)
        {
            if (view == null)
            {
                continue;
            }

            // Soft-heal Scene View camera size/pivot that can make ScreenToGrid explode.
            if (view.size <= 0f || float.IsNaN(view.size) || float.IsInfinity(view.size))
            {
                view.size = 10f;
            }

            Vector3 pivot = view.pivot;
            if (!IsFinite(pivot))
            {
                view.pivot = Vector3.zero;
            }

            view.Repaint();
        }

        Debug.Log(
            "Castlevania 2D: switched to View tool and validated Scene View size/pivot. " +
            "If OverflowException/GUIClips spam continues, close Window → 2D → Tile Palette.");
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
#endif
