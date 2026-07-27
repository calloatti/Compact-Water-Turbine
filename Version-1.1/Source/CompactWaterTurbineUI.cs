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
      _root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/WaterMoverFragment");

      _root.Q("ToggleWrapper").ToggleDisplayStyle(false);

      _dropHeightLabel = new Label();
      _dropHeightLabel.AddToClassList("entity-panel__text");
      _dropHeightLabel.style.marginBottom = 2;
      _dropHeightLabel.style.backgroundColor = UnityEngine.Color.clear;

      _realFlowLabel = new Label();
      _realFlowLabel.AddToClassList("entity-panel__text");
      _realFlowLabel.style.marginBottom = 10;
      _realFlowLabel.style.backgroundColor = UnityEngine.Color.clear;

      VisualElement efficiencyWrapper = _root.Q<VisualElement>("EfficiencyWrapper");

      efficiencyWrapper.Insert(0, _dropHeightLabel);
      efficiencyWrapper.Insert(1, _realFlowLabel);

      _flowRateLabel = _root.Q<Label>("EfficiencyLabel");
      _flowRateSlider = _root.Q<PreciseSlider>("Efficiency");
      _flowRateSlider.SetValueChangedCallback(SetFlowRate);
      _flowRateSlider.SetStepWithoutNotify(0.01f);

      _synchronizeToggle = new Toggle
      {
        text = _loc.T("Building.CompactWaterTurbine.Synchronize")
      };
      _synchronizeToggle.AddToClassList("game-toggle");
      _synchronizeToggle.AddToClassList("entity-panel__text");
      _synchronizeToggle.AddToClassList("entity-panel__toggle");
      _synchronizeToggle.style.marginTop = 10;
      _synchronizeToggle.RegisterValueChangedCallback(ToggleSynchronization);

      efficiencyWrapper.Add(_synchronizeToggle);

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
        _dropHeightLabel.text = _loc.T(_dropHeightPhrase, currentHead);
        _realFlowLabel.text = _loc.T(_realFlowPhrase, _turbine.EffectiveFlowRate);

        _flowRateLabel.text = _loc.T(_flowRatePhrase, _turbine.FlowRate);
        _flowRateSlider.UpdateValuesWithoutNotify(_turbine.FlowRate, _turbine.MaxFlowRate);
        _flowRateSlider.SetMarker(_turbine.EffectiveFlowRate);

        _synchronizeToggle.SetValueWithoutNotify(_turbine.IsSynchronized);
      }
    }

    private void SetFlowRate(float value)
    {
      _turbine.SetFlowRateAndSynchronize(value);
    }

    private void ToggleSynchronization(ChangeEvent<bool> changeEvent)
    {
      _turbine.ToggleSynchronization(changeEvent.newValue);
      _flowRateSlider.SetValueWithoutNotify(_turbine.FlowRate);
    }
  }
}