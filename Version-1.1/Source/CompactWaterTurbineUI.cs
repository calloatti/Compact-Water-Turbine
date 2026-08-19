using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using UnityEngine.UIElements;

namespace Calloatti.CompactWaterTurbine
{
  public class CompactWaterTurbineFragment : IEntityPanelFragment
  {
    private readonly VisualElementLoader _visualElementLoader;
    private readonly ILoc _loc;

    private VisualElement _root;
    private CompactWaterTurbine _turbine;

    private Label _dropHeightLabel;
    private Label _realFlowLabel;
    private Label _flowRateLabel;
    private PreciseSlider _flowRateSlider;
    private Toggle _synchronizeToggle;

    private readonly Phrase _flowRatePhrase = Phrase.New("Buildings.MechanicalPump.FlowRate").FormatFlow<float>("F2");
    private readonly Phrase _dropHeightPhrase = Phrase.New("Building.CompactWaterTurbine.CurrentDrop").FormatDistance<float>("F2");
    private readonly Phrase _realFlowPhrase = Phrase.New("Building.CompactWaterTurbine.RealFlow").FormatFlow<float>("F2");

    public CompactWaterTurbineFragment(VisualElementLoader visualElementLoader, ILoc loc)
    {
      _visualElementLoader = visualElementLoader;
      _loc = loc;
    }

    public VisualElement InitializeFragment()
    {
      _root = _visualElementLoader.LoadVisualElement("CompactWaterTurbine/TurbinePanel");

      _dropHeightLabel = _root.Q<Label>("DropHeightLabel");
      _realFlowLabel = _root.Q<Label>("RealFlowLabel");
      _flowRateLabel = _root.Q<Label>("EfficiencyLabel");
      _flowRateSlider = _root.Q<PreciseSlider>("Efficiency");
      _synchronizeToggle = _root.Q<Toggle>("SynchronizeToggle");

      _flowRateSlider.SetValueChangedCallback(SetFlowRate);
      _flowRateSlider.SetStepWithoutNotify(0.01f);
      _synchronizeToggle.RegisterValueChangedCallback(ToggleSynchronization);

      _root.ToggleDisplayStyle(visible: false);
      return _root;
    }

    public void ShowFragment(BaseComponent entity)
    {
      CompactWaterTurbine component = entity.GetComponent<CompactWaterTurbine>();
      if (component != null)
      {
        _turbine = component;
        _root.ToggleDisplayStyle(visible: true);
      }
    }

    public void ClearFragment()
    {
      _turbine = null;
      _root.ToggleDisplayStyle(visible: false);
    }

    public void UpdateFragment()
    {
      if (_turbine != null)
      {
        float currentHead = _turbine.GetCurrentHead();

        if (_dropHeightLabel != null)
          _dropHeightLabel.text = _loc.T(_dropHeightPhrase, currentHead);

        if (_realFlowLabel != null)
          _realFlowLabel.text = _loc.T(_realFlowPhrase, _turbine.EffectiveFlowRate);

        if (_flowRateLabel != null)
          _flowRateLabel.text = _loc.T(_flowRatePhrase, _turbine.FlowRate);

        if (_flowRateSlider != null)
        {
          _flowRateSlider.UpdateValuesWithoutNotify(_turbine.FlowRate, _turbine.MaxFlowRate);
          _flowRateSlider.SetMarker(_turbine.EffectiveFlowRate);
        }

        if (_synchronizeToggle != null)
          _synchronizeToggle.SetValueWithoutNotify(_turbine.IsSynchronized);
      }
    }

    private void SetFlowRate(float value)
    {
      if (_turbine != null)
      {
        _turbine.SetFlowRateAndSynchronize(value);
      }
    }

    private void ToggleSynchronization(ChangeEvent<bool> changeEvent)
    {
      if (_turbine != null)
      {
        _turbine.ToggleSynchronization(changeEvent.newValue);
        if (_flowRateSlider != null)
        {
          _flowRateSlider.SetValueWithoutNotify(_turbine.FlowRate);
        }
      }
    }
  }
}