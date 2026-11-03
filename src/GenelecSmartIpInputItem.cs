using System;
using System.Collections.Generic;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// A selectable input item that maps to one or more Smart IP physical inputs
  /// </summary>
  public class GenelecSmartIpInputItem : ISelectableItem
  {
    private readonly Action<GenelecSmartIpInputItem> selectAction;
    private bool isSelected;

    /// <summary>
    /// Creates the item
    /// </summary>
    public GenelecSmartIpInputItem(string key, string name, IList<string> smartIpInputs, Action<GenelecSmartIpInputItem> selectAction)
    {
      Key = key;
      Name = name;
      SmartIpInputs = smartIpInputs;
      this.selectAction = selectAction;
    }

    /// <inheritdoc />
    public string Key { get; private set; }

    /// <inheritdoc />
    public string Name { get; private set; }

    /// <summary>
    /// Smart IP identifiers (A, AoIP01, AoIP02) sent when this item is selected
    /// </summary>
    public IList<string> SmartIpInputs { get; private set; }

    /// <inheritdoc />
    public event EventHandler ItemUpdated;

    /// <inheritdoc />
    public bool IsSelected
    {
      get { return isSelected; }
      set
      {
        if (isSelected == value) return;
        isSelected = value;
        var handler = ItemUpdated;
        if (handler != null) handler(this, EventArgs.Empty);
      }
    }

    /// <inheritdoc />
    public void Select()
    {
      selectAction(this);
    }
  }
}
