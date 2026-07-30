Include ..\AGENTS.md

# Compact Water Turbine — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `compactwaterturbine`
- **Namespace:** `Calloatti.compactwaterturbine`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** removes `Timberborn.BlueprintSystem`, includes `Timberborn.CoreUI`, `Timberborn.WaterSystem`, `Timberborn.TimbermeshAnimations`
- **ModId:** `Calloatti.CompactWaterTurbine`
- **Min Game Version:** 1.0.0.0 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Adds a compact 1x1 water turbine building with custom particle effects, UI, and synchronization logic.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `ModStarter.cs` | Entry point — `IModStarter` |
| `CompactWaterTurbine.cs` | Core turbine component |
| `CompactWaterTurbineConfigurator.cs` | DI configurator |
| `CompactWaterTurbineParticleController.cs` | Particle effects for water flow |
| `CompactWaterTurbinePatches.cs` | Harmony patches |
| `CompactWaterTurbineSynchronizer.cs` | Power synchronization logic |
| `CompactWaterTurbineUI.cs` | UI elements |

## Version Folders
- `Version-1.0` — targets game 1.0.x.x
- `Version-1.1` — targets game 1.1.x.x
