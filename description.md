# Compact Water Turbine: Technical Architecture and Logic Overview

## 1. Initialization and Dependency Injection
The mod initializes through `ModStarter`, which applies Harmony patches to modify vanilla game behaviors. The dependency injection is handled by `CompactWaterTurbineConfigurator`, which registers the core components (`CompactWaterTurbine`, `CompactWaterTurbineParticleController`, `CompactWaterTurbineSynchronizer`) as well as the UI fragment and template decorations into Timberborn's Entity Component System (ECS).

## 2. Core Water Mechanics and Power Generation
The `CompactWaterTurbine` class acts as the main physical simulation component.
* **Head Calculation:** It samples the water levels every 1.0 seconds to calculate the "head" (water drop). This is determined by taking the intake's water height (including pressure overflow multiplied by an overflow factor) and subtracting the exhaust's water height.
* **Water Movement:** If the head exceeds the specified `MinWaterDrop` plus a small activation buffer (0.6f), the flow state becomes active. The turbine smoothly ramps up to the user-defined `FlowRate` over 4.0 seconds.
* **Contamination Handling:** When moving water, the turbine reads the current contamination level at the intake and precisely divides the removed and added water into clean and contaminated pools using the `IWaterService`.
* **Power Output:** Power generation is continuously updated. The output multiplier is calculated by multiplying a "head multiplier" (how close the current head is to the `MaxWaterDrop`) by a "flow multiplier" (current flow versus max flow).

## 3. Adjacent Synchronization
To make managing large arrays of turbines easier, the mod includes a `CompactWaterTurbineSynchronizer`.
* When a player adjusts the flow rate on a synchronized turbine, the synchronizer scans the 3D grid around the machine's occupied blocks using `Deltas.Neighbors4Vector3Int`.
* It propagates the new flow rate to any horizontally adjacent turbines that also have synchronization enabled, using a queue to ensure all connected components are updated.

## 4. Visuals and Particle Physics
The visual representation of the water exiting the turbine is highly dynamic:
* **Particle Controller:** `CompactWaterTurbineParticleController` continuously monitors the turbine's effective flow rate and contamination percentage. It dynamically interpolates the particle speed, density, and gravity modifier based on the flow percentage.
* **Dynamic Coloring:** It blends the particle's start color between the vanilla clean water color and a hardcoded badwater color (RGBA: 0.46, 0.15, 0.09) based on the exact contamination ratio.
* **Harmony Patching:** To ensure particles don't fall indefinitely or despawn too early due to the modified gravity, `Patch_WaterOutputParticleLength_UpdateLifetime` recalculates the required lifetime of the vanilla `WaterOutputParticleLength` component. It forces the particles to visually hit the surface exactly at the 75% mark of their calculated lifespan.

## 5. User Interface
Player interaction is handled by the `CompactWaterTurbineFragment`.
* It injects a custom interface into the standard Timberborn entity panel using the game's UI Toolkit layout system.
* The panel displays real-time statistics for "Current Drop" and "Real Flow" using localized phrases.
* It provides a precise slider to set the desired Flow Rate and a toggle switch to enable or disable the neighbor synchronization feature.