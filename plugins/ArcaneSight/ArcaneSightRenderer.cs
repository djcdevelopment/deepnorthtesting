namespace ArcaneSight;

using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneSight.Configuration;
using UnityEngine;

public sealed class ArcaneSightRenderer {
  const string LampName = "comfy-arcane-sight-lamp";
  static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

  readonly Dictionary<Renderer, MaterialPropertyBlock> previousBlocks = new();
  readonly List<ScannedObject> activeMarkers = new();
  readonly List<Light> createdLamps = new();

  bool active;
  float nextScanTime;
  GUIStyle? labelStyle;
  GUIStyle? boxStyle;
  Texture2D? backgroundTexture;

  public bool IsActive => active;

  public void Toggle() {
    if (active) Disable();
    else Enable();
  }

  public void Enable() {
    if (active) return;
    active = true;
    nextScanTime = 0f;
    Scan();
  }

  public void Disable() {
    CleanupVisuals();
    activeMarkers.Clear();
    active = false;
  }

  public void Update() {
    if (!active) return;
    if (Time.time >= nextScanTime) {
      nextScanTime = Time.time + 0.45f;
      Scan();
    }
  }

  void InitStyles() {
    if (labelStyle != null && boxStyle != null) return;

    backgroundTexture = new Texture2D(1, 1);
    backgroundTexture.SetPixel(0, 0, new Color(0.06f, 0.09f, 0.08f, 0.88f));
    backgroundTexture.Apply();

    boxStyle = new GUIStyle(GUI.skin.box) {
      normal = { background = backgroundTexture },
      border = new RectOffset(4, 4, 4, 4),
      padding = new RectOffset(8, 8, 4, 4)
    };

    labelStyle = new GUIStyle(GUI.skin.label) {
      fontSize = 12,
      fontStyle = FontStyle.Bold,
      alignment = TextAnchor.MiddleCenter,
      richText = true,
      wordWrap = false
    };
  }

  public void OnGUI() {
    if (!active || activeMarkers.Count == 0) return;
    var camera = Camera.main ?? GameCamera.instance?.GetComponent<Camera>();
    if (camera == null) return;

    InitStyles();
    var player = Player.m_localPlayer;
    var playerPos = player != null ? player.transform.position : camera.transform.position;

    var sorted = activeMarkers
        .Where(m => m.Host != null)
        .OrderBy(m => Vector3.Distance(playerPos, m.Host.transform.position))
        .Take(32);

    foreach (var marker in sorted) {
      if (marker.Host == null) continue;
      var worldPos = marker.Host.transform.position + Vector3.up * marker.LabelHeight;
      var screenPos = camera.WorldToScreenPoint(worldPos);

      // Behind the camera
      if (screenPos.z <= 0.1f) continue;

      var dist = Vector3.Distance(playerPos, marker.Host.transform.position);
      var x = screenPos.x;
      var y = Screen.height - screenPos.y;

      // Offscreen culling
      if (x < -100f || x > Screen.width + 100f || y < -50f || y > Screen.height + 50f) continue;

      string distTag = $" <color=#94A3B8>({Math.Round(dist, 1)}m)</color>";
      string content = $"<color=#{ColorUtility.ToHtmlStringRGB(marker.HighlightColor)}>{marker.Title}</color>{distTag}\n"
                     + $"<color=#CBD5E1>{marker.Subtitle}</color>";

      var guiContent = new GUIContent(content);
      var size = labelStyle!.CalcSize(guiContent);
      var rect = new Rect(x - (size.x / 2f) - 6f, y - size.y - 4f, size.x + 12f, size.y + 6f);

      GUI.Box(rect, GUIContent.none, boxStyle);
      GUI.Label(rect, content, labelStyle);
    }
  }

  void Scan() {
    var player = Player.m_localPlayer;
    if (player == null) return;

    var center = player.transform.position;
    float maxDist = ArcaneSightConfig.ScanRadius.Value;
    var found = new List<ScannedObject>();

    // 1. Scan Portals
    if (ArcaneSightConfig.ShowPortals.Value) {
      foreach (var portal in UnityEngine.Object.FindObjectsOfType<TeleportWorld>()) {
        if (portal == null) continue;
        float d = Vector3.Distance(center, portal.transform.position);
        if (d > maxDist) continue;

        string tag = portal.GetText();
        if (string.IsNullOrWhiteSpace(tag)) tag = "Unassigned";
        bool connected = portal.TargetFound();
        string status = connected ? "<color=#34D399>[Connected]</color>" : "<color=#F87171>[Unconnected]</color>";

        found.Add(new ScannedObject {
          Host = portal.gameObject,
          Title = $"?? Portal: \"{tag}\"",
          Subtitle = status,
          HighlightColor = connected ? new Color(0.35f, 0.85f, 1.0f) : new Color(1.0f, 0.45f, 0.45f),
          LabelHeight = 2.4f
        });
      }
    }

    // 2. Scan Containers / Chests
    if (ArcaneSightConfig.ShowContainers.Value) {
      foreach (var container in UnityEngine.Object.FindObjectsOfType<Container>()) {
        if (container == null) continue;
        float d = Vector3.Distance(center, container.transform.position);
        if (d > maxDist) continue;

        var inv = container.GetInventory();
        if (inv == null) continue;

        int total = inv.NrOfItems();
        int maxSlots = inv.GetWidth() * inv.GetHeight();
        float fillPct = maxSlots > 0 ? (float)total / maxSlots : 0f;

        string topItems = "";
        if (d <= 14.0f && total > 0) {
          var top = inv.GetAllItems()
              .GroupBy(i => i.m_shared.m_name)
              .OrderByDescending(g => g.Sum(i => i.m_stack))
              .Take(3)
              .Select(g => $"{Localization.instance.Localize(g.Key)} x{g.Sum(i => i.m_stack)}");
          topItems = $" · {string.Join(", ", top)}";
        }

        string fillTag = fillPct >= 1.0f ? "<color=#F87171>Full</color>" : $"{total}/{maxSlots} slots";
        var chestColor = fillPct >= 1.0f ? new Color(1.0f, 0.6f, 0.2f) : new Color(0.95f, 0.8f, 0.35f);

        found.Add(new ScannedObject {
          Host = container.gameObject,
          Title = $"?? Chest: {fillTag}",
          Subtitle = total == 0 ? "Empty" : $"Contains {total} items{topItems}",
          HighlightColor = chestColor,
          LabelHeight = 1.2f
        });
      }
    }

    // 3. Scan Signs
    if (ArcaneSightConfig.ShowSigns.Value) {
      foreach (var sign in UnityEngine.Object.FindObjectsOfType<Sign>()) {
        if (sign == null) continue;
        float d = Vector3.Distance(center, sign.transform.position);
        if (d > maxDist) continue;

        string text = sign.GetText();
        if (string.IsNullOrWhiteSpace(text)) continue;

        found.Add(new ScannedObject {
          Host = sign.gameObject,
          Title = "?? Inscribed Sign",
          Subtitle = $"\"{text.Trim()}\"",
          HighlightColor = new Color(0.85f, 0.95f, 0.75f),
          LabelHeight = 0.9f
        });
      }
    }

    // 4. Scan Processing Stations (Beehives, Fermenters, Smelters)
    if (ArcaneSightConfig.ShowProcessingStations.Value) {
      foreach (var beehive in UnityEngine.Object.FindObjectsOfType<Beehive>()) {
        if (beehive == null) continue;
        float d = Vector3.Distance(center, beehive.transform.position);
        if (d > maxDist) continue;

        int honey = beehive.GetHoneyLevel();
        found.Add(new ScannedObject {
          Host = beehive.gameObject,
          Title = "?? Beehive",
          Subtitle = $"Honey: {honey}/{beehive.m_maxHoney}",
          HighlightColor = new Color(1.0f, 0.85f, 0.25f),
          LabelHeight = 1.3f
        });
      }

      foreach (var fermenter in UnityEngine.Object.FindObjectsOfType<Fermenter>()) {
        if (fermenter == null) continue;
        float d = Vector3.Distance(center, fermenter.transform.position);
        if (d > maxDist) continue;

        string status = fermenter.GetStatus().ToString();
        found.Add(new ScannedObject {
          Host = fermenter.gameObject,
          Title = "?? Fermenter",
          Subtitle = status,
          HighlightColor = new Color(0.78f, 0.45f, 0.95f),
          LabelHeight = 1.6f
        });
      }

      foreach (var smelter in UnityEngine.Object.FindObjectsOfType<Smelter>()) {
        if (smelter == null) continue;
        float d = Vector3.Distance(center, smelter.transform.position);
        if (d > maxDist) continue;

        float fuel = smelter.GetFuel();
        int queue = smelter.GetQueueSize();
        found.Add(new ScannedObject {
          Host = smelter.gameObject,
          Title = $"?? {smelter.m_name}",
          Subtitle = $"Fuel: {Math.Round(fuel)}/{smelter.m_maxFuel} · Ore: {queue}/{smelter.m_maxOre}",
          HighlightColor = new Color(1.0f, 0.45f, 0.2f),
          LabelHeight = 2.8f
        });
      }
    }

    // 5. Scan Creator OS / Comfy Quest Charm Bindings
    if (ArcaneSightConfig.ShowQuestBindings.Value) {
      foreach (var wear in WearNTear.GetAllInstances()) {
        if (wear == null) continue;
        float d = Vector3.Distance(center, wear.transform.position);
        if (d > maxDist) continue;

        var view = wear.GetComponent<ZNetView>();
        var zdo = view != null ? view.GetZDO() : null;
        if (zdo == null) continue;

        string packId = zdo.GetString("comfyQuestRuntime.packId", "");
        string expId = zdo.GetString("comfyQuestRuntime.experienceId", "");
        if (!string.IsNullOrEmpty(packId) || !string.IsNullOrEmpty(expId)) {
          found.Add(new ScannedObject {
            Host = wear.gameObject,
            Title = "? Quest Shorter Binding",
            Subtitle = $"{packId} · {expId} (ZDO {zdo.m_uid})",
            HighlightColor = new Color(0.72f, 0.43f, 1.0f),
            LabelHeight = 2.0f
          });
        }
      }
    }

    activeMarkers.Clear();
    activeMarkers.AddRange(found);

    // Apply or refresh lights
    if (ArcaneSightConfig.EnableEtherealGlow.Value) {
      ApplyGlow(found);
    }
  }

  void ApplyGlow(List<ScannedObject> markers) {
    foreach (var marker in markers) {
      if (marker.Host == null) continue;
      var existing = marker.Host.transform.Find(LampName);
      Light lamp;
      if (existing == null) {
        var lampObj = new GameObject(LampName) { hideFlags = HideFlags.DontSave };
        lampObj.transform.SetParent(marker.Host.transform, false);
        lamp = lampObj.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.shadows = LightShadows.None;
        createdLamps.Add(lamp);
      } else {
        lamp = existing.GetComponent<Light>();
      }

      if (lamp != null) {
        lamp.color = marker.HighlightColor;
        lamp.intensity = 0.95f;
        lamp.range = 5.0f;
      }
    }
  }

  void CleanupVisuals() {
    foreach (var lamp in createdLamps) {
      if (lamp != null && lamp.gameObject != null) {
        UnityEngine.Object.Destroy(lamp.gameObject);
      }
    }
    createdLamps.Clear();

    foreach (var pair in previousBlocks) {
      try {
        if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
      } catch { }
    }
    previousBlocks.Clear();
  }

  sealed class ScannedObject {
    public GameObject Host = null!;
    public string Title = "";
    public string Subtitle = "";
    public Color HighlightColor = Color.cyan;
    public float LabelHeight = 1.5f;
  }
}

