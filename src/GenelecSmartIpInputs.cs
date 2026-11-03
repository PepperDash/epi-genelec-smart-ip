using System;
using System.Collections.Generic;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Input collection exposed through IHasInputs&lt;string&gt;
  /// </summary>
  public class GenelecSmartIpInputs : ISelectableItems<string>
  {
    private string currentItem = string.Empty;

    /// <inheritdoc />
    public event EventHandler ItemsUpdated;

    /// <inheritdoc />
    public event EventHandler CurrentItemChanged;

    /// <inheritdoc />
    public Dictionary<string, ISelectableItem> Items { get; set; } = new Dictionary<string, ISelectableItem>();

    /// <inheritdoc />
    public string CurrentItem
    {
      get { return currentItem; }
      set
      {
        if (currentItem == value) return;
        currentItem = value;

        foreach (var item in Items)
        {
          item.Value.IsSelected = item.Key == value;
        }

        var handler = CurrentItemChanged;
        if (handler != null) handler(this, EventArgs.Empty);
      }
    }

    /// <summary>
    /// Raises ItemsUpdated
    /// </summary>
    public void NotifyItemsUpdated()
    {
      var handler = ItemsUpdated;
      if (handler != null) handler(this, EventArgs.Empty);
    }
  }
}
