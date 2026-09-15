namespace ArcaneSight;

using BepInEx;
using ArcaneSight.Configuration;
using UnityEngine;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class ArcaneSightPlugin : BaseUnityPlugin {
  public const string PluginGuid = "com.djcdevelopment.valheim.arcanesight";
  public const string PluginName = "ArcaneSight";
  public const string PluginVersion = "1.0.0";

  readonly ArcaneSightRenderer renderer = new();

  void Awake() {
    ArcaneSightConfig.Initialize(Config);
    Logger.LogInfo($"{PluginName} v{PluginVersion} initialized. Press [{ArcaneSightConfig.ToggleKey.Value}] to toggle Arcane Sight.");
  }

  void Update() {
    // Only handle input when in world and not typing in chat / console
    if (Player.m_localPlayer == null) return;
    if (Chat.instance != null && Chat.instance.HasFocus()) return;
    if (Console.instance != null && Console.instance.IsCheatsEnabled() && Console.IsVisible()) return;
    if (TextInput.IsVisible()) return;

    if (Input.GetKeyDown(ArcaneSightConfig.ToggleKey.Value)) {
      renderer.Toggle();
      string stateText = renderer.IsActive
          ? "<color=#38BDF8>? Arcane Sight: Activated</color>"
          : "<color=#94A3B8>? Arcane Sight: Deactivated</color>";

      if (MessageHud.instance != null) {
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, stateText);
      }
    }

    renderer.Update();
  }

  void OnGUI() {
    renderer.OnGUI();
  }

  void OnDestroy() {
    renderer.Disable();
  }
}
