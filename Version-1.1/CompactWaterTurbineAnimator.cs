using Timberborn.BaseComponentSystem;
using Timberborn.TickSystem;
using Timberborn.TimbermeshAnimations;
using UnityEngine;

namespace Calloatti.CompactWaterTurbine
{
  public class CompactWaterTurbineAnimator : TickableComponent, IAwakableComponent
  {
    private CompactWaterTurbine _turbine;
    private IAnimator _animator;

    public void Awake()
    {
      _turbine = GetComponent<CompactWaterTurbine>();
      _animator = GetComponentInChildren<IAnimator>(includeInactive: true);
    }

    public override void Tick()
    {
      if (_animator != null)
      {
        if (_turbine.EffectiveFlowRate > 0f)
        {
          _animator.Enabled = true;
          // Scales animation speed matching the current percentage of maximum load
          _animator.Speed = Mathf.Max(0.1f, _turbine.EffectiveFlowRate / _turbine.MaxFlowRate);
        }
        else
        {
          _animator.Enabled = false;
          _animator.Speed = 0f;
        }
      }
    }
  }
}