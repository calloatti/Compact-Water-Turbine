using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using UnityEngine.UIElements;
using UnityEngine;

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

    private readonly Phrase _flowRatePhrase = Phrase.New("Building.CompactWaterTurbine.FlowRate").FormatFlow<float>("F2");
    private readonly Phrase _dropHeightPhrase = Phrase.New("Building.CompactWaterTurbine.CurrentDrop").FormatDistance<float>("F2");
    private readonly Phrase _realFlowPhrase = Phrase.New("Building.CompactWaterTurbine.RealFlow").FormatFlow<float>("F2");

    public CompactWaterTurbineFragment(VisualElementLoader visualElementLoader, ILoc loc)
    {
      _visualElementLoader = visualElementLoader;
      _loc = loc;
    }

    public VisualElement InitializeFragment()
    {
      // 1. Programmatically recreate the Native NineSlice UI frame (the green box)
      NineSliceVisualElement rootFrame = new NineSliceVisualElement();
      rootFrame.name = "WaterMoverFragment";
      rootFrame.AddToClassList("entity-sub-panel");
      rootFrame.AddToClassList("bg-sub-box--green");
      _root = rootFrame;

      // 2. Initialize Programmatic Text Labels
      _dropHeightLabel = new Label();
      _dropHeightLabel.AddToClassList("entity-panel__text");
      _dropHeightLabel.style.marginTop = 4;
      _dropHeightLabel.style.marginBottom = 2;
      _dropHeightLabel.style.backgroundColor = Color.clear;
      _root.Add(_dropHeightLabel);

      _realFlowLabel = new Label();
      _realFlowLabel.AddToClassList("entity-panel__text");
      _realFlowLabel.style.marginBottom = 10;
      _realFlowLabel.style.backgroundColor = Color.clear;
      _root.Add(_realFlowLabel);

      // 3. Recreate the "EfficiencyWrapper" from your UXML exactly as-is
      VisualElement efficiencyWrapper = new VisualElement();
      efficiencyWrapper.name = "EfficiencyWrapper";
      efficiencyWrapper.style.marginTop = 4;
      efficiencyWrapper.style.marginBottom = 4;

      _flowRateLabel = new Label();
      _flowRateLabel.name = "EfficiencyLabel";
      _flowRateLabel.AddToClassList("entity-panel__text");
      _flowRateLabel.style.marginTop = 6;
      _flowRateLabel.style.marginBottom = 2;
      efficiencyWrapper.Add(_flowRateLabel);

      // Instantiate the Custom PreciseSlider natively
      _flowRateSlider = new PreciseSlider();
      _flowRateSlider.name = "Efficiency";
      _flowRateSlider.SetValueChangedCallback(SetFlowRate);
      _flowRateSlider.SetStepWithoutNotify(0.01f);

      // Apply the aggressive layout constraints to force the slider track to stretch
      _flowRateSlider.style.flexGrow = 1f;
      Slider internalSlider = _flowRateSlider.Q<Slider>();
      if (internalSlider != null)
      {
        internalSlider.style.flexGrow = 1f;
        internalSlider.style.width = new StyleLength(StyleKeyword.Auto);
        internalSlider.style.minWidth = 150f;
      }

      efficiencyWrapper.Add(_flowRateSlider);
      _root.Add(efficiencyWrapper);

      // 4. Construct the Neighbor Synchronization Toggle Feature
      _synchronizeToggle = new Toggle
      {
        text = _loc.T("Building.CompactWaterTurbine.Synchronize")
      };
      _synchronizeToggle.AddToClassList("game-toggle");
      _synchronizeToggle.AddToClassList("entity-panel__text");
      _synchronizeToggle.AddToClassList("entity-panel__toggle");
      _synchronizeToggle.style.marginTop = 10;
      _synchronizeToggle.style.marginBottom = 4;
      _synchronizeToggle.RegisterValueChangedCallback(ToggleSynchronization);
      _root.Add(_synchronizeToggle);

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