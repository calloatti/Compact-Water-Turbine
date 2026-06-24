using HarmonyLib;
using System;
using Timberborn.WaterBuildings;
using Timberborn.WaterBuildingsUI;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  [HarmonyPatch(typeof(WaterOutputParticleLength), "UpdateLifetime")]
  public static class Patch_WaterOutputParticleLength_UpdateLifetime
  {
    private const float UnityGravity = 9.81f;

    [HarmonyPrefix]
    public static bool Prefix(
      WaterOutputParticleLength __instance,
      ref ParticleSystem.MainModule ____particlesMainModule,
      WaterOutput ____waterOutput,
      WaterOutputParticleSpec ____spec)
    {
      if (!__instance.TryGetComponent(out CompactWaterTurbine turbine))
      {
        return true;
      }

      float flowPercentage = turbine.MaxFlowRate > 0f ? (turbine.EffectiveFlowRate / turbine.MaxFlowRate) : 0f;
      flowPercentage = Mathf.Clamp(flowPercentage, 0.01f, 1.0f);

      // We recalculate the dynamic gravity here so the particle math matches the controller's physics
      float dynamicGravityModifier = Mathf.Lerp(
        CompactWaterTurbineParticleController.MinFlowGravity,
        CompactWaterTurbineParticleController.MaxFlowGravity,
        flowPercentage
      );

      float verticalDistance = Mathf.Max(0f, ____waterOutput.DistanceToGround + ____spec.SpawnOffset);

      float effectiveGravity = UnityGravity * dynamicGravityModifier;
      float calculatedLifetime = Mathf.Sqrt((2f * verticalDistance) / effectiveGravity);

      // Forces the surface impact to happen exactly at the 75% mark of the particle's lifespan
      calculatedLifetime /= 0.75f;

      ____particlesMainModule.startLifetime = Math.Max(____spec.MinimumLifetime, calculatedLifetime);

      return false;
    }
  }
}