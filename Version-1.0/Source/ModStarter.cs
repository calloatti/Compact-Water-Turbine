using HarmonyLib;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class ModStarter : IModStarter
  {
    public static readonly string ModId = "Calloatti.CompactWaterTurbine";

    public void StartMod(IModEnvironment modEnvironment)
    {
      // Instantiate Harmony using your unique Mod ID
      new Harmony(ModId).PatchAll();

      Debug.Log($"[{ModId}] Mod started successfully and Harmony patches applied.");
    }
  }
}