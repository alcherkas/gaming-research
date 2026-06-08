using UnityEngine;

// Assigns the procedural player sprite + color at runtime so the prefab/scene
// never references a generated sprite asset.
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    public Color Color = new Color(0.30f, 0.85f, 1f);

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null)
            sr.sprite = SpriteFactory.Disc();
        sr.color = Color;
        sr.sortingOrder = 10;
    }

    public void SetColor(Color c)
    {
        Color = c;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;
        var sem = GetComponent<StatusEffectManager>();
        if (sem != null) sem.SetBaselineColor(c);
    }
}
