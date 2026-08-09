using System;
using UnityEngine;

[Serializable]
public sealed class SceneLayoutSnapshotFile
{
    public string capturedAtUtc;
    public string scenePath;
    public SceneLayoutObjectEntry[] objects;
}

[Serializable]
public sealed class SceneLayoutObjectEntry
{
    public string hierarchyPath;
    public bool isActive = true;
    public SceneLayoutVector3 position;
    public SceneLayoutVector3 eulerAngles;
    public SceneLayoutVector3 scale;
    public int sortingOrder = int.MinValue;
}

[Serializable]
public struct SceneLayoutVector3
{
    public float x;
    public float y;
    public float z;

    public SceneLayoutVector3(Vector3 value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
