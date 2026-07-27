using HarmonyLib;
using Timberborn.WaterBuildingsUI;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  [HarmonyPatch]
  public static class CompactWaterTurbinePatches
  {
    [HarmonyPatch(typeof(WaterOutputParticleLength), "UpdateLifetime")]
    [HarmonyPrefix]
    public static bool UpdateLifetime_Prefix(WaterOutputParticleLength __instance, ref ParticleSystem.MainModule ____particlesMainModule)
    {
      // Exactly like your pump patch! If the instance is registered in our registry, bypass vanilla.
      if (__instance != null && CompactWaterTurbineParticleController.CustomParticleLengths.TryGetValue(__instance, out float customLifetime))
      {
        ____particlesMainModule.startLifetime = customLifetime;
        return false;
      }
      return true; // Let normal water buildings process vanilla raycasts
    }
  }
}