using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class ModStarter : IModStarter
  {
    public static readonly string ModId = "calloatti.CompactWaterTurbine";

    public void StartMod(IModEnvironment modEnvironment)
    {
      Debug.Log($"[{ModId}] Mod started successfully.");
    }
  }
}