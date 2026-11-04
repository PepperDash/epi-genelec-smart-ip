// linked into the nullable-enabled test project; the plugin itself does not use nullable annotations
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Pure-logic input helpers (no Crestron dependencies so they can be unit tested)
  /// </summary>
  public static class GenelecSmartIpInputMatch
  {
    /// <summary>
    /// Input names PUT /audio/inputs accepts (Smart IP API Documentation v1 rev 0.8.4, section 4.8)
    /// </summary>
    public static readonly string[] ValidInputNames = { "A", "AoIP01", "AoIP02" };

    /// <summary>
    /// The entries of <paramref name="inputs"/> that are not valid Smart IP input names (compared
    /// case-insensitively), in their original spelling. Null/empty entries count as invalid; a null or
    /// empty list returns no entries (an item with no inputs is rejected earlier, when inputs are built)
    /// </summary>
    public static List<string> InvalidInputNames(IEnumerable<string> inputs)
    {
      return (inputs ?? Enumerable.Empty<string>())
          .Where(s => string.IsNullOrWhiteSpace(s)
                      || !ValidInputNames.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
          .Select(s => s ?? "(null)")
          .ToList();
    }

    /// <summary>
    /// True when both lists name the same Smart IP inputs, ignoring order, duplicates and case
    /// (the device reports e.g. ["AoIP01","AoIP02"] for an item configured as ["AoIP02","AoIP01"])
    /// </summary>
    public static bool SameSet(IEnumerable<string> a, IEnumerable<string> b)
    {
      var left = new HashSet<string>((a ?? Enumerable.Empty<string>()).Where(s => s != null), StringComparer.OrdinalIgnoreCase);
      var right = new HashSet<string>((b ?? Enumerable.Empty<string>()).Where(s => s != null), StringComparer.OrdinalIgnoreCase);
      return left.SetEquals(right);
    }

    /// <summary>
    /// Finds the configured input item key that <paramref name="wanted"/> names (case-insensitive).
    /// Returns null when <paramref name="wanted"/> is empty (feature off) or names no item
    /// (<paramref name="notFound"/> set)
    /// </summary>
    public static string ResolveKey(string wanted, IEnumerable<string> itemKeys, out bool notFound)
    {
      notFound = false;
      if (string.IsNullOrWhiteSpace(wanted)) return null;

      var match = (itemKeys ?? Enumerable.Empty<string>())
          .FirstOrDefault(k => string.Equals(k, wanted.Trim(), StringComparison.OrdinalIgnoreCase));
      if (match == null) notFound = true;
      return match;
    }
  }
}
