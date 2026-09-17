# Changelog - SealCompanion

All notable changes to the **SealCompanion** mod are documented in this file.

---

## [1.0.0] - 2026-09-17

### Added
- **Feeding & Diet System**: Integrated all fish types (`Fish1` through `Fish12`, `FishRaw`, and `FishCooked`) into seal consumable diets.
- **Taming Mechanics**: Calming loop evaluating threat states (fire, combat, noise), progressive taming ticks every 3s, and soothing gold heart emote particles (`<3`).
- **Follow & Command System**: Interact (`E`) toggles Follow and Stay commands, Shift + `E` custom naming, and petting interactions with `<Name> loves you`.
- **Fishing Retriever Assist**: 35% probability (configurable 0–100%) for a happy tamed seal to retrieve hooked fish directly to your boat or shore, preventing stamina drain and line breakage.
- **Master Defense & Combat Resilience**: 160 Base HP companion durability, blubber damage resistance (blunt, frost, pierce), custom 35 physical damage bite weapon (`seal_bite_attack`), and proactive master defense perimeter scans.
- **Hydration & Hot Tub Behavior**: 10-minute dry-land grace period before 2.5x hunger acceleration kicks in; idle base pathfinding into built hot tubs (`piece_bathtub`) for relaxation.
- **Boat Navigation**: Boarding on rafts and longships, with automated disembarkation when nearing shore (< 2.8m water depth).
- **Archify Architecture Maps**: Fully rendered interactive HTML system map and high-resolution visual-check renders.
