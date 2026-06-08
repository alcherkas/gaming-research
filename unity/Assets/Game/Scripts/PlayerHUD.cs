using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space HUD attached to each player showing health bar, mana bar, and status effect icons.
/// Created programmatically — no prefab or asset references needed.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    private Health _health;
    private SpellSystem _spell;
    private StatusEffectManager _sem;

    private Image _healthFill;
    private Image _manaFill;
    private Image _burnIcon;
    private Image _freezeIcon;
    private Image _poisonIcon;
    private Image _shockIcon;

    private Canvas _canvas;
    private const float BarWidth = 1.2f;
    private const float BarHeight = 0.12f;
    private const float ManaBarHeight = 0.08f;
    private const float HudYOffset = 0.75f;
    private const float IconSize = 0.15f;

    private void Start()
    {
        _health = GetComponent<Health>();
        _spell = GetComponent<SpellSystem>();
        _sem = GetComponent<StatusEffectManager>();

        BuildHUD();

        if (_health != null) _health.OnHealthChangedEvent += OnHealthChanged;
        if (_spell != null) _spell.OnManaChangedEvent += OnManaChanged;
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnHealthChangedEvent -= OnHealthChanged;
        if (_spell != null) _spell.OnManaChangedEvent -= OnManaChanged;
    }

    private void BuildHUD()
    {
        // Root canvas in world space
        var canvasGo = new GameObject("HUD_Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, HudYOffset, 0f);
        canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 1f); // World-space scale

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 20;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(BarWidth / 0.01f, (BarHeight + ManaBarHeight + 0.1f) / 0.01f);

        // Health bar
        float healthY = 0f;
        _healthFill = CreateBar(canvasGo.transform, "HealthBar", healthY, BarWidth, BarHeight,
            new Color(0.15f, 0.15f, 0.15f, 0.6f),  // background
            new Color(0.9f, 0.2f, 0.2f, 0.9f));      // fill

        // Mana bar (below health)
        float manaY = -(BarHeight + 0.03f);
        _manaFill = CreateBar(canvasGo.transform, "ManaBar", manaY, BarWidth, ManaBarHeight,
            new Color(0.1f, 0.1f, 0.2f, 0.6f),
            new Color(0.2f, 0.4f, 0.95f, 0.9f));

        // Status effect icons (row below mana bar)
        float iconY = manaY - (ManaBarHeight + 0.06f);
        float startX = -(IconSize * 2f + 0.03f * 1.5f);
        _burnIcon = CreateIcon(canvasGo.transform, "BurnIcon", startX, iconY, new Color(1f, 0.3f, 0.1f, 0.85f));
        _freezeIcon = CreateIcon(canvasGo.transform, "FreezeIcon", startX + IconSize + 0.03f, iconY, new Color(0.2f, 0.6f, 1f, 0.85f));
        _poisonIcon = CreateIcon(canvasGo.transform, "PoisonIcon", startX + (IconSize + 0.03f) * 2f, iconY, new Color(0.3f, 0.8f, 0.3f, 0.85f));
        _shockIcon = CreateIcon(canvasGo.transform, "ShockIcon", startX + (IconSize + 0.03f) * 3f, iconY, new Color(1f, 0.9f, 0.2f, 0.85f));

        // Initially hidden
        _burnIcon.gameObject.SetActive(false);
        _freezeIcon.gameObject.SetActive(false);
        _poisonIcon.gameObject.SetActive(false);
        _shockIcon.gameObject.SetActive(false);
    }

    private Image CreateBar(Transform parent, string name, float localY, float width, float height, Color bgColor, Color fillColor)
    {
        float scaleFactor = 1f / 0.01f; // Inverse of canvas world scale

        // Background
        var bgGo = new GameObject(name + "_BG");
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchoredPosition = new Vector2(0f, localY * scaleFactor);
        bgRect.sizeDelta = new Vector2(width * scaleFactor, height * scaleFactor);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = bgColor;

        // Fill
        var fillGo = new GameObject(name + "_Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = fillColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        return fillImg;
    }

    private Image CreateIcon(Transform parent, string name, float localX, float localY, Color color)
    {
        float scaleFactor = 1f / 0.01f;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(localX * scaleFactor, localY * scaleFactor);
        rect.sizeDelta = new Vector2(IconSize * scaleFactor, IconSize * scaleFactor);
        var img = go.AddComponent<Image>();
        img.color = color;

        return img;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (_healthFill != null && max > 0f)
            _healthFill.fillAmount = current / max;
    }

    private void OnManaChanged(float current, float max)
    {
        if (_manaFill != null && max > 0f)
            _manaFill.fillAmount = current / max;
    }

    private bool _lastBurn;
    private bool _lastFreeze;
    private bool _lastPoison;
    private bool _lastShock;

    private void Update()
    {
        if (_sem == null) return;

        bool isBurning = _sem.IsBurning;
        if (isBurning != _lastBurn)
        {
            _burnIcon.gameObject.SetActive(isBurning);
            _lastBurn = isBurning;
        }

        bool isFrozen = _sem.IsFrozen;
        if (isFrozen != _lastFreeze)
        {
            _freezeIcon.gameObject.SetActive(isFrozen);
            _lastFreeze = isFrozen;
        }

        bool isPoisoned = _sem.IsPoisoned;
        if (isPoisoned != _lastPoison)
        {
            _poisonIcon.gameObject.SetActive(isPoisoned);
            _lastPoison = isPoisoned;
        }

        bool isShocked = _sem.IsShocked;
        if (isShocked != _lastShock)
        {
            _shockIcon.gameObject.SetActive(isShocked);
            _lastShock = isShocked;
        }
    }
}
