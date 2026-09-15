namespace ArcaneSight.Configuration;

using BepInEx.Configuration;
using UnityEngine;

public static class ArcaneSightConfig {
  public static ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;
  public static ConfigEntry<float> ScanRadius { get; private set; } = null!;
  public static ConfigEntry<bool> EnableEtherealGlow { get; private set; } = null!;
  public static ConfigEntry<bool> ShowPortals { get; private set; } = null!;
  public static ConfigEntry<bool> ShowContainers { get; private set; } = null!;
  public static ConfigEntry<bool> ShowSigns { get; private set; } = null!;
  public static ConfigEntry<bool> ShowProcessingStations { get; private set; } = null!;
  public static ConfigEntry<bool> ShowStructuralHealth { get; private set; } = null!;
  public static ConfigEntry<bool> ShowQuestBindings { get; private set; } = null!;

  public static void Initialize(ConfigFile config) {
    ToggleKey = config.Bind(
        "General", "ToggleKey", KeyCode.F7,
        "Keyboard shortcut to toggle Arcane Sight on or off.");

    ScanRadius = config.Bind(
        "General", "ScanRadius", 28.0f,
        "Maximum distance in meters to detect and highlight world objects.");

    EnableEtherealGlow = config.Bind(
        "Visuals", "EnableEtherealGlow", true,
        "Project soft ethereal rune lights and emission onto highlighted interactive objects.");

    ShowPortals = config.Bind(
        "Detection", "ShowPortals", true,
        "Inspect and display portal tags and connection states in 3D.");

    ShowContainers = config.Bind(
        "Detection", "ShowContainers", true,
        "Inspect chest capacity, used slots, and top item previews.");

    ShowSigns = config.Bind(
        "Detection", "ShowSigns", true,
        "Render legible 3D rune text directly above wooden and stone signs.");

    ShowProcessingStations = config.Bind(
        "Detection", "ShowProcessingStations", true,
        "Display status of beehives, fermenters, smelters, and kilns.");

    ShowStructuralHealth = config.Bind(
        "Detection", "ShowStructuralHealth", false,
        "Display structural stability percentage and health bars on building pieces.");

    ShowQuestBindings = config.Bind(
        "Detection", "ShowQuestBindings", true,
        "Detect and display Creator OS / Comfy Quest charm bindings and ritual shrines.");
  }
}
