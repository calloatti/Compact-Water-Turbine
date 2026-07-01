using HarmonyLib;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class ModStarter : IModStarter
  {
    public static readonly string ModId = "calloatti.CompactWaterTurbine";

    public void StartMod(IModEnvironment modEnvironment)
    {
      // Instantiate Harmony using your unique Mod ID
      Harmony harmony = new Harmony(ModId);

      // Tell Harmony to find all [HarmonyPatch] attributes in your mod and apply them
      harmony.PatchAll();

      Debug.Log($"[{ModId}] Mod started successfully and Harmony patches applied.");
    }
  }
}