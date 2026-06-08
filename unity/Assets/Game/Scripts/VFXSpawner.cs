using UnityEngine;

/// <summary>
/// Handles local-only visual effects (VFX) procedurally.
/// All GameObjects spawned are non-networked and auto-destroy.
/// </summary>
public static class VFXSpawner
{
    public static void SpawnHitFlash(Vector2 pos, Color color)
    {
        var go = new GameObject("VFX_HitFlash");
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        sr.sortingOrder = 18;

        var anim = go.AddComponent<VFXEffect>();
        anim.Lifetime = 0.15f;
        anim.UpdateCallback = (t) =>
        {
            sr.color = new Color(color.r, color.g, color.b, 1f - t);
        };
    }

    public static void SpawnDashTrail(Vector2 startPos, Vector2 dir, Color color)
    {
        // Spawn 3 afterimages along the dash path, spaced backward from start position
        for (int i = 0; i < 3; i++)
        {
            Vector2 pos = startPos - dir * (0.5f * (i + 1));
            var go = new GameObject("VFX_DashTrail_" + i);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = new Color(color.r, color.g, color.b, 0.45f);
            sr.sortingOrder = 8; // Render behind characters

            var anim = go.AddComponent<VFXEffect>();
            anim.Lifetime = 0.3f;
            float initialAlpha = 0.45f;
            anim.UpdateCallback = (t) =>
            {
                sr.color = new Color(color.r, color.g, color.b, initialAlpha * (1f - t));
            };
        }
    }

    public static void SpawnSpellImpact(Vector2 pos, DamageType type)
    {
        Color color = type switch
        {
            DamageType.Fire => new Color(1f, 0.35f, 0.15f),
            DamageType.Ice => new Color(0.2f, 0.6f, 1f),
            DamageType.Lightning => new Color(1f, 0.9f, 0.2f),
            DamageType.Poison => new Color(0.3f, 0.8f, 0.2f),
            _ => Color.white
        };

        var root = new GameObject("VFX_SpellImpact");
        root.transform.position = pos;

        // Spawn 6 small discs arranged in a ring
        int count = 6;
        var srs = new SpriteRenderer[count];
        var dirs = new Vector2[count];
        float radiusStart = 0.15f;
        float radiusEnd = 0.8f;

        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var go = new GameObject("ImpactDisc_" + i);
            go.transform.SetParent(root.transform);
            go.transform.localPosition = dirs[i] * radiusStart;
            go.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Disc();
            sr.color = color;
            sr.sortingOrder = 14;
            srs[i] = sr;
        }

        var anim = root.AddComponent<VFXEffect>();
        anim.Lifetime = 0.25f;
        anim.UpdateCallback = (t) =>
        {
            float currentRadius = Mathf.Lerp(radiusStart, radiusEnd, t);
            for (int i = 0; i < count; i++)
            {
                if (srs[i] != null)
                {
                    srs[i].transform.localPosition = dirs[i] * currentRadius;
                    srs[i].color = new Color(color.r, color.g, color.b, 1f - t);
                    srs[i].transform.localScale = new Vector3(0.22f * (1f - t * 0.4f), 0.22f * (1f - t * 0.4f), 1f);
                }
            }
        };
    }
}

/// <summary>
/// Helper MonoBehaviour to drive procedural animations for VFX.
/// </summary>
public class VFXEffect : MonoBehaviour
{
    public float Lifetime = 0.5f;
    public System.Action<float> UpdateCallback;

    private float _timer = 0f;

    private void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Lifetime);
        UpdateCallback?.Invoke(t);

        if (_timer >= Lifetime)
        {
            Destroy(gameObject);
        }
    }
}
