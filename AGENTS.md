Include ..\AGENTS.md

# Compact Water Turbine — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `compactwaterturbine`
- **Namespace:** `Calloatti.compactwaterturbine`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** `Timberborn.BlueprintSystem` is publicized via `CommonModSettings.props`, with `DoNotPublicize` for `ComponentSpec.EqualityContract`/`PrintMembers` (record-inheritance CS0507 fix — see csproj); csproj also adds `Timberborn.CoreUI`, `Timberborn.WaterSystem`, `Timberborn.TimbermeshAnimations`
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
- `Version-1.0` — targets game 1.0.x.x (Unity 6000.3.6f1)
- `Version-1.1` — targets game 1.1.x.x (Unity 6000.5.5f1)

## Asset Bundle Build Process

### Unity Projects (per-version)
- `C:\Users\calloatti\source\repos\timberborn-modding-6000.3.6f1` — for 1.0 bundles
- `C:\Users\calloatti\source\repos\timberborn-modding-6000.5.5f1` — for 1.1 bundles
- Each is a separate Unity Editor install with matching Unity version (from game's UnityPlayer.dll)

### Source Layout (in each Unity project)
```
Assets/Mods/Compact Water Turbine/
├── manifest.json
└── AssetBundles/
    └── Resources/UI/Views/CompactWaterTurbine/TurbinePanel.uxml
```

### Build Script
- `Version-1.0/unitybuild.ps1` and `Version-1.1/unitybuild.ps1`
- Auto-detects Unity version from folder name (Version-1.0 → 6000.3.6f1)
- Downloads Unity via Hub if missing (`--headless install --version --changeset`)
- Runs `NativeModBuilderBatch.Build` with `-mod "Compact Water Turbine" -compatibilityVersion 1.0/1.1`
- Copies built bundles from `Documents\Timberborn\Mods\Compact Water Turbine\version-<ver>\AssetBundles` → repo `Version-<ver>\AssetBundles\`

### Key Points
- **Never touch** `Documents\Timberborn\Mods\` (deploy folder)
- Mac bundles require Mac Build Support module installed in Unity Hub
- AssetBundles built with `buildCode: false`, `buildWindowsAssetBundle: true`, `buildMacAssetBundle: true`
- Copy step excludes `*_win`, `*_mac`, `*.manifest` — only copies `Resources/` tree + `manifest.json`

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.
