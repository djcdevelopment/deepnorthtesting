namespace SealCompanion.HUD;

using System;
using SealCompanion.Components;
using SealCompanion.Configuration;
using UnityEngine;

public static class SealCuriosityPanel
{
    public enum DisplayMode
    {
        Hidden,
        Training,
        Tamed,
        Lost
    }

    private static readonly Vector3[] s_mapCorners = new Vector3[4];
    private static readonly Color s_readyColor = new Color(0.94f, 0.94f, 0.94f, 1.0f);
    private static readonly Color s_frostColor = new Color(0.33f, 0.84f, 1.0f, 1.0f);
    private static readonly Color s_goldColor = new Color(1.0f, 0.69f, 0.18f, 1.0f);
    private static readonly Color s_orangeColor = new Color(1.0f, 0.45f, 0.08f, 1.0f);
    private static readonly Color s_redColor = new Color(1.0f, 0.20f, 0.22f, 1.0f);
    private static readonly Color s_greenColor = new Color(0.08f, 0.78f, 0.30f, 1.0f);
    private static readonly Color s_backgroundColor = new Color(0.025f, 0.025f, 0.025f, 0.90f);
    private static readonly Color s_barBgColor = new Color(0.08f, 0.10f, 0.12f, 0.95f);

    private static DisplayMode s_mode = DisplayMode.Hidden;
    private static float s_stateChangeTime = 0f;
    private static bool s_dismissed = false;
    private static SealBehaviorController? s_activeSeal = null;

    private static GUIStyle? s_titleStyle;
    private static GUIStyle? s_mainStyle;
    private static GUIStyle? s_detailStyle;
    private static GUIStyle? s_barTextStyle;
    private static GUIStyle? s_statusStyle;
    private static GUIStyle? s_closeStyle;

    public static SealBehaviorController? ActiveSeal => s_activeSeal;

    public static void SetActiveSeal(SealBehaviorController seal)
    {
        s_activeSeal = seal;
        s_dismissed = false;
        SetMode(DisplayMode.Training);
    }

    public static void OnSealTamed(SealBehaviorController seal)
    {
        s_activeSeal = seal;
        s_dismissed = false;
        SetMode(DisplayMode.Tamed);
    }

    public static void OnCuriosityLost(SealBehaviorController seal)
    {
        s_activeSeal = seal;
        s_dismissed = false;
        SetMode(DisplayMode.Lost);
    }

    private static void SetMode(DisplayMode mode)
    {
        s_mode = mode;
        s_stateChangeTime = Time.unscaledTime;
    }

    public static void Draw()
    {
        if (s_dismissed) return;
        if (Player.m_localPlayer == null || Hud.instance == null) return;
        if (Hud.instance.m_rootObject == null || !Hud.instance.m_rootObject.activeInHierarchy) return;

        // Auto-find closest curious seal if none active or current one finished
        if (s_mode == DisplayMode.Hidden || s_activeSeal == null || !s_activeSeal.IsCurious)
        {
            if (s_mode != DisplayMode.Tamed && s_mode != DisplayMode.Lost)
            {
                SealBehaviorController? nearbyCurious = FindClosestCuriousSeal(Player.m_localPlayer.transform.position);
                if (nearbyCurious != null)
                {
                    SetActiveSeal(nearbyCurious);
                }
                else
                {
                    return;
                }
            }
        }

        // Handle auto-closing for completed or lost sessions
        float elapsed = Time.unscaledTime - s_stateChangeTime;
        if (s_mode == DisplayMode.Tamed && elapsed > 15f)
        {
            s_mode = DisplayMode.Hidden;
            return;
        }
        if (s_mode == DisplayMode.Lost && elapsed > 8f)
        {
            s_mode = DisplayMode.Hidden;
            return;
        }

        if (s_mode == DisplayMode.Hidden) return;

        EnsureStyles();
        float scale = Mathf.Clamp(Screen.height / 1080.0f, 0.80f, 1.45f);
        Rect card = GetCardRect(scale);
        float border = Mathf.Max(3.0f, 4.0f * scale);
        Color accent = GetAccentColor();

        // Outer border & dark background
        DrawSolidRect(card, accent);
        DrawSolidRect(
            new Rect(card.x + border, card.y + border, card.width - border * 2.0f, card.height - border * 2.0f),
            s_backgroundColor
        );

        UpdateFontSizes(scale);
        DrawCardContents(card, scale, accent);
    }

    private static SealBehaviorController? FindClosestCuriousSeal(Vector3 playerPos)
    {
        SealBehaviorController? best = null;
        float bestDist = 50f;

        foreach (var instance in SealBehaviorController.Instances)
        {
            if (instance != null && instance.IsCurious)
            {
                float dist = Vector3.Distance(playerPos, instance.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = instance;
                }
            }
        }

        return best;
    }

    private static void DrawCardContents(Rect card, float scale, Color accent)
    {
        float inset = 10.0f * scale;
        float width = card.width - inset * 2.0f;

        Rect titleRect = new Rect(card.x + inset, card.y + 8.0f * scale, width, 22.0f * scale);
        Rect timerRect = new Rect(card.x + inset, card.y + 30.0f * scale, width, 36.0f * scale);
        Rect barBgRect = new Rect(card.x + inset + 4.0f * scale, card.y + 70.0f * scale, width - 8.0f * scale, 16.0f * scale);
        Rect detailRect = new Rect(card.x + inset, card.y + 92.0f * scale, width, 20.0f * scale);
        Rect statusRect = new Rect(card.x + inset, card.y + 114.0f * scale, width, 20.0f * scale);

        s_titleStyle!.normal.textColor = accent;
        s_mainStyle!.normal.textColor = accent;
        s_detailStyle!.normal.textColor = s_readyColor;

        switch (s_mode)
        {
            case DisplayMode.Training:
                DrawTrainingState(titleRect, timerRect, barBgRect, detailRect, statusRect, scale);
                break;

            case DisplayMode.Tamed:
                DrawTamedState(titleRect, timerRect, barBgRect, detailRect, statusRect, scale);
                DrawCloseButton(card, scale);
                break;

            case DisplayMode.Lost:
                DrawLostState(titleRect, timerRect, barBgRect, detailRect, statusRect, scale);
                DrawCloseButton(card, scale);
                break;
        }
    }

    private static void DrawTrainingState(Rect titleRect, Rect timerRect, Rect barBgRect, Rect detailRect, Rect statusRect, float scale)
    {
        if (s_activeSeal == null) return;

        // Title with star rank indicator
        int star = s_activeSeal.SealStarLevel;
        string stars = star > 0 ? $" ({new string('★', star)})" : string.Empty;
        GUI.Label(titleRect, $"SEAL TRAINING{stars}", s_titleStyle);

        // Timer
        int remainingSec = Mathf.Max(0, Mathf.CeilToInt(s_activeSeal.TameSessionTimeRemaining));
        string timerText = $"{remainingSec / 60:00}:{remainingSec % 60:00}";
        GUI.Label(timerRect, timerText, s_mainStyle);

        // Curiosity Gauge Bar
        float curiosityPercent = s_activeSeal.CuriosityPercent;
        Color barColor = curiosityPercent > 0.50f ? s_frostColor : (curiosityPercent > 0.25f ? s_goldColor : s_redColor);

        // Bar background
        DrawSolidRect(barBgRect, s_barBgColor);
        // Bar border
        float barBorder = 1.0f * scale;
        DrawSolidRect(new Rect(barBgRect.x, barBgRect.y, barBgRect.width, barBorder), barColor * 0.6f);
        DrawSolidRect(new Rect(barBgRect.x, barBgRect.yMax - barBorder, barBgRect.width, barBorder), barColor * 0.6f);

        // Bar fill
        float fillWidth = Mathf.Clamp(barBgRect.width * curiosityPercent, 0f, barBgRect.width);
        DrawSolidRect(new Rect(barBgRect.x, barBgRect.y, fillWidth, barBgRect.height), barColor);

        // Text inside bar
        s_barTextStyle!.normal.textColor = s_readyColor;
        GUI.Label(barBgRect, $"CURIOSITY  {Mathf.RoundToInt(curiosityPercent * 100f)}%", s_barTextStyle);

        // Fish count detail
        int lured = s_activeSeal.FishLuredCount;
        GUI.Label(detailRect, $"FISH LURED: {lured}  ·  SPACING ≥ 8M", s_detailStyle);

        // Status guidance row
        s_statusStyle!.normal.textColor = s_activeSeal.LastGatePassed ? s_frostColor : s_orangeColor;
        string status = string.IsNullOrEmpty(s_activeSeal.StatusMessage) ? "KITE ALONG COASTLINE" : s_activeSeal.StatusMessage;
        GUI.Label(statusRect, status, s_statusStyle);
    }

    private static void DrawTamedState(Rect titleRect, Rect timerRect, Rect barBgRect, Rect detailRect, Rect statusRect, float scale)
    {
        GUI.Label(titleRect, "SEAL TAMED!", s_titleStyle);
        GUI.Label(timerRect, "LOYAL", s_mainStyle);

        DrawSolidRect(barBgRect, s_greenColor);
        s_barTextStyle!.normal.textColor = s_backgroundColor;
        GUI.Label(barBgRect, "COMPANION BOND FORMED", s_barTextStyle);

        GUI.Label(detailRect, "DEFENDS MASTER & ASSISTS FISHING", s_detailStyle);
        s_statusStyle!.normal.textColor = s_greenColor;
        GUI.Label(statusRect, "CLICK OR PET TO INTERACT", s_statusStyle);
    }

    private static void DrawLostState(Rect titleRect, Rect timerRect, Rect barBgRect, Rect detailRect, Rect statusRect, float scale)
    {
        GUI.Label(titleRect, "CURIOSITY LOST", s_titleStyle);
        GUI.Label(timerRect, "-- : --", s_mainStyle);

        DrawSolidRect(barBgRect, s_barBgColor);
        s_barTextStyle!.normal.textColor = s_redColor;
        GUI.Label(barBgRect, "SEAL WANDERED OFF", s_barTextStyle);

        GUI.Label(detailRect, "DROP SPACED FISH BEFORE TIMER DIES", s_detailStyle);
        s_statusStyle!.normal.textColor = s_orangeColor;
        GUI.Label(statusRect, "TRY AGAIN ON NEXT WILD SEAL", s_statusStyle);
    }

    private static void DrawCloseButton(Rect card, float scale)
    {
        Rect closeRect = new Rect(card.xMax - 28.0f * scale, card.y + 6.0f * scale, 22.0f * scale, 22.0f * scale);
        if (GUI.Button(closeRect, "X", s_closeStyle))
        {
            s_dismissed = true;
            s_mode = DisplayMode.Hidden;
        }
    }

    private static Rect GetCardRect(float scale)
    {
        float width = 236.0f * scale;
        float height = 142.0f * scale;
        float x = Screen.width - width - 20.0f * scale;
        float y = 285.0f * scale;

        Minimap minimap = Minimap.instance;
        if (minimap != null && minimap.m_smallRoot != null && minimap.m_smallRoot.activeInHierarchy && minimap.m_mapImageSmall != null)
        {
            minimap.m_mapImageSmall.rectTransform.GetWorldCorners(s_mapCorners);
            float mapRight = s_mapCorners[0].x;
            float mapBottom = s_mapCorners[0].y;
            for (int i = 1; i < s_mapCorners.Length; i++)
            {
                mapRight = Mathf.Max(mapRight, s_mapCorners[i].x);
                mapBottom = Mathf.Min(mapBottom, s_mapCorners[i].y);
            }

            x = mapRight - width;
            y = Screen.height - mapBottom + 10.0f * scale;
        }

        x = Mathf.Clamp(x, 8.0f, Screen.width - width - 8.0f);
        y = Mathf.Clamp(y, 8.0f, Screen.height - height - 8.0f);
        return new Rect(x, y, width, height);
    }

    private static Color GetAccentColor()
    {
        return s_mode switch
        {
            DisplayMode.Tamed => s_greenColor,
            DisplayMode.Lost => s_redColor,
            DisplayMode.Training => (s_activeSeal != null && s_activeSeal.CuriosityPercent < 0.25f) ? s_orangeColor : s_frostColor,
            _ => s_readyColor
        };
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void EnsureStyles()
    {
        if (s_titleStyle != null) return;

        s_titleStyle = CreateCenteredStyle(FontStyle.Bold, s_frostColor);
        s_mainStyle = CreateCenteredStyle(FontStyle.Bold, s_frostColor);
        s_detailStyle = CreateCenteredStyle(FontStyle.Normal, s_readyColor);
        s_barTextStyle = CreateCenteredStyle(FontStyle.Bold, s_readyColor);
        s_statusStyle = CreateCenteredStyle(FontStyle.Normal, s_frostColor);
        s_closeStyle = CreateCenteredStyle(FontStyle.Bold, s_readyColor);
        s_closeStyle.hover.textColor = s_redColor;
        s_closeStyle.active.textColor = s_orangeColor;
    }

    private static void UpdateFontSizes(float scale)
    {
        if (s_titleStyle == null) return;

        s_titleStyle.fontSize = Mathf.RoundToInt(15.0f * scale);
        s_mainStyle!.fontSize = Mathf.RoundToInt(26.0f * scale);
        s_detailStyle!.fontSize = Mathf.RoundToInt(11.0f * scale);
        s_barTextStyle!.fontSize = Mathf.RoundToInt(10.0f * scale);
        s_statusStyle!.fontSize = Mathf.RoundToInt(11.0f * scale);
        s_closeStyle!.fontSize = Mathf.RoundToInt(14.0f * scale);
    }

    private static GUIStyle CreateCenteredStyle(FontStyle fontStyle, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = fontStyle,
            clipping = TextClipping.Clip,
            normal = { textColor = color }
        };
    }
}
