using UnityEngine;

// Generates placeholder sprites at runtime (programmer-art only — no external assets).
//
// Performance reference: both shapes live in ONE shared 64x32 texture (a manual runtime atlas) and
// therefore share a single material, so every floor/wall/player SpriteRenderer batches together
// (SRP Batcher + same texture => ~1 draw call for the whole scene). Building visuals procedurally
// also keeps the saved scene free of generated sprite-asset references (headless-build safe).
public static class SpriteFactory
{
    const int Cell = 32;
    static Texture2D _atlas;
    static Sprite _square;
    static Sprite _disc;

    public static Sprite Square()
    {
        EnsureAtlas();
        if (_square == null)
            _square = Sprite.Create(_atlas, new Rect(0, 0, Cell, Cell), new Vector2(0.5f, 0.5f), Cell);
        return _square;
    }

    public static Sprite Disc()
    {
        EnsureAtlas();
        if (_disc == null)
            _disc = Sprite.Create(_atlas, new Rect(Cell, 0, Cell, Cell), new Vector2(0.5f, 0.5f), Cell);
        return _disc;
    }

    static void EnsureAtlas()
    {
        if (_atlas != null) return;

        _atlas = new Texture2D(Cell * 2, Cell, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[Cell * 2 * Cell];

        float r = Cell * 0.5f;
        for (int y = 0; y < Cell; y++)
        {
            int row = y * (Cell * 2);
            for (int x = 0; x < Cell; x++)
            {
                // Left cell: solid square.
                px[row + x] = new Color32(255, 255, 255, 255);

                // Right cell: filled disc.
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= r * r;
                px[row + Cell + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        _atlas.SetPixels32(px);
        _atlas.Apply();
    }
}
