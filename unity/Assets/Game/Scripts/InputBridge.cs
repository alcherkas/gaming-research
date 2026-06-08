using UnityEngine;
using UnityEngine.InputSystem;

// Unified input that reads BOTH the new Input System (Gamepad/Keyboard) and the legacy Input Manager.
// On Steam Deck, Steam Input's emulated controller is frequently visible to the LEGACY Input Manager
// even when Unity's new Input System backend doesn't enumerate it on Linux. Reading both and merging
// makes movement/menus work regardless of which backend sees the pad.
//
// Includes a headless/mock input override layer to support autonomous testing (AGY).
public static class InputBridge
{
    static float _prevNavY;

    // Headless / AI Mock input values
    public static Vector2 MockMoveValue = Vector2.zero;
    public static bool MockDashValue = false;
    public static bool MockAttackValue = false;
    public static bool MockCastFireValue = false;
    public static bool MockCastIceValue = false;
    public static bool MockCastLightningValue = false;
    public static bool MockCastPoisonValue = false;
    public static bool MockSubmitValue = false;

    public static void ResetMockInputs()
    {
        MockMoveValue = Vector2.zero;
        MockDashValue = false;
        MockAttackValue = false;
        MockCastFireValue = false;
        MockCastIceValue = false;
        MockCastLightningValue = false;
        MockCastPoisonValue = false;
        MockSubmitValue = false;
    }

    public static Vector2 Move()
    {
        if (MockMoveValue.sqrMagnitude > 0.01f)
            return Vector2.ClampMagnitude(MockMoveValue, 1f);

        Vector2 v = Vector2.zero;

        var gp = Gamepad.current;
        if (gp != null) v += gp.leftStick.ReadValue();

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        v.x += Input.GetAxisRaw("Horizontal");
        v.y += Input.GetAxisRaw("Vertical");
#endif
        return Vector2.ClampMagnitude(v, 1f);
    }

    // Edge-detected vertical menu navigation: returns +1 (down/next), -1 (up/prev), or 0.
    public static int MenuNav()
    {
        float y = 0f;
        var gp = Gamepad.current;
        if (gp != null)
        {
            y = gp.leftStick.ReadValue().y;
            float d = gp.dpad.ReadValue().y;
            if (Mathf.Abs(d) > 0.5f) y = d;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        float legacy = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(legacy) > 0.5f) y = legacy;
#endif
        int edge = 0;
        if (y < -0.5f && _prevNavY >= -0.5f) edge = 1;
        else if (y > 0.5f && _prevNavY <= 0.5f) edge = -1;
        _prevNavY = y;

        var kb = Keyboard.current;
        if (kb != null && kb.downArrowKey.wasPressedThisFrame) edge = 1;
        if (kb != null && kb.upArrowKey.wasPressedThisFrame) edge = -1;
        return edge;
    }

    public static bool Submit()
    {
        if (MockSubmitValue) return true;

        var gp = Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame)) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0) ||
            Input.GetKeyDown(KeyCode.JoystickButton1))
            return true;
#endif
        return false;
    }

    public static bool Dash()
    {
        if (MockDashValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) return true;
#endif
        return false;
    }

    public static bool Attack()
    {
        if (MockAttackValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonWest.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.jKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2)) return true;
#endif
        return false;
    }

    public static bool CastFire()
    {
        if (MockCastFireValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonNorth.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3)) return true;
#endif
        return false;
    }

    public static bool CastIce()
    {
        if (MockCastIceValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonEast.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton1)) return true;
#endif
        return false;
    }

    public static bool CastLightning()
    {
        if (MockCastLightningValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.leftShoulder.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.uKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.JoystickButton4)) return true;
#endif
        return false;
    }

    public static bool CastPoison()
    {
        if (MockCastPoisonValue) return true;

        var gp = Gamepad.current;
        if (gp != null && gp.rightShoulder.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.iKey.wasPressedThisFrame) return true;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.JoystickButton5)) return true;
#endif
        return false;
    }

    // One-line diagnostic of what each backend currently sees (for the menu + Player.log).
    public static string Status()
    {
        var gp = Gamepad.current;
        string s = "newGamepad=" + (gp != null ? "yes" : "NO");
        if (gp != null) s += " stickY=" + gp.leftStick.ReadValue().y.ToString("0.00");
#if ENABLE_LEGACY_INPUT_MANAGER
        s += "  legacy: joysticks=" + Input.GetJoystickNames().Length +
             " V=" + Input.GetAxisRaw("Vertical").ToString("0.00");
#else
        s += "  legacy=off";
#endif
        return s;
    }
}
