using UnityEngine;

// Builds a top-down room (floor + perimeter walls) procedurally, with optional doorway gaps.
// A doorway splits a wall into two segments leaving a centered gap of DoorWidth.
public static class RoomBuilder
{
    public const float DoorWidth = 3f;
    const float WallThickness = 1f;

    [System.Flags]
    public enum Doors { None = 0, N = 1, S = 2, E = 4, W = 8 }

    public static void Build(string name, Vector2 center, Vector2 size, Doors doors,
                             Color floorColor, Color wallColor, Transform parent)
    {
        var root = new GameObject(name);
        if (parent != null) root.transform.SetParent(parent, false);

        Quad(root.transform, "Floor", center, size, floorColor, false, -10);

        float hx = size.x * 0.5f, hy = size.y * 0.5f, t = WallThickness;
        WallH(root.transform, "Wall_N", new Vector2(center.x, center.y + hy), size.x + t, t, (doors & Doors.N) != 0, wallColor);
        WallH(root.transform, "Wall_S", new Vector2(center.x, center.y - hy), size.x + t, t, (doors & Doors.S) != 0, wallColor);
        WallV(root.transform, "Wall_E", new Vector2(center.x + hx, center.y), size.y + t, t, (doors & Doors.E) != 0, wallColor);
        WallV(root.transform, "Wall_W", new Vector2(center.x - hx, center.y), size.y + t, t, (doors & Doors.W) != 0, wallColor);
    }

    static void WallH(Transform parent, string name, Vector2 pos, float length, float thickness, bool door, Color color)
    {
        if (!door) { Quad(parent, name, pos, new Vector2(length, thickness), color, true, 0); return; }
        float seg = (length - DoorWidth) * 0.5f;
        float off = DoorWidth * 0.5f + seg * 0.5f;
        Quad(parent, name + "_a", new Vector2(pos.x - off, pos.y), new Vector2(seg, thickness), color, true, 0);
        Quad(parent, name + "_b", new Vector2(pos.x + off, pos.y), new Vector2(seg, thickness), color, true, 0);
    }

    static void WallV(Transform parent, string name, Vector2 pos, float length, float thickness, bool door, Color color)
    {
        if (!door) { Quad(parent, name, pos, new Vector2(thickness, length), color, true, 0); return; }
        float seg = (length - DoorWidth) * 0.5f;
        float off = DoorWidth * 0.5f + seg * 0.5f;
        Quad(parent, name + "_a", new Vector2(pos.x, pos.y - off), new Vector2(thickness, seg), color, true, 0);
        Quad(parent, name + "_b", new Vector2(pos.x, pos.y + off), new Vector2(thickness, seg), color, true, 0);
    }

    static void Quad(Transform parent, string name, Vector2 pos, Vector2 size, Color color, bool collider, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        sr.sortingOrder = order;
        if (collider) go.AddComponent<BoxCollider2D>();
    }
}
