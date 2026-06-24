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
      Harmony harmony = new Harmony(ModId);
      harmony.PatchAll();
      Debug.Log($"[{ModId}] Harmony patches applied successfully.");
    }
  }
}