using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class InputMatchTests
{
  [Theory]
  [InlineData(new[] { "AoIP01" }, new[] { "AoIP01" }, true)]
  [InlineData(new[] { "AoIP02", "AoIP01" }, new[] { "AoIP01", "AoIP02" }, true)]   // order
  [InlineData(new[] { "aoip01" }, new[] { "AoIP01" }, true)]                        // case
  [InlineData(new[] { "AoIP01", "AoIP01" }, new[] { "AoIP01" }, true)]             // duplicates
  [InlineData(new[] { "AoIP01" }, new[] { "AoIP01", "AoIP02" }, false)]            // subset is not equal
  [InlineData(new[] { "A" }, new[] { "AoIP01" }, false)]
  [InlineData(new string[0], new[] { "AoIP01" }, false)]
  public void SameSet_Compares_As_Sets(string[] current, string[] wanted, bool expected)
  {
    GenelecSmartIpInputMatch.SameSet(current, wanted).Should().Be(expected);
  }

  [Fact]
  public void SameSet_Treats_Null_As_Empty()
  {
    GenelecSmartIpInputMatch.SameSet(null, new string[0]).Should().BeTrue();
    GenelecSmartIpInputMatch.SameSet(null, new[] { "A" }).Should().BeFalse();
  }

  private static readonly string[] Keys = { "danteLeft", "danteRight", "analog" };

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void ResolveKey_Empty_Means_Feature_Off(string? wanted)
  {
    GenelecSmartIpInputMatch.ResolveKey(wanted, Keys, out var notFound).Should().BeNull();
    notFound.Should().BeFalse();
  }

  [Theory]
  [InlineData("danteLeft", "danteLeft")]
  [InlineData("DANTELEFT", "danteLeft")]
  [InlineData(" analog ", "analog")]
  public void ResolveKey_Matches_Configured_Key_Case_Insensitively(string wanted, string expected)
  {
    GenelecSmartIpInputMatch.ResolveKey(wanted, Keys, out var notFound).Should().Be(expected);
    notFound.Should().BeFalse();
  }

  [Theory]
  [InlineData("A")]
  [InlineData("AoIP01,AoIP02")]
  [InlineData("aoip01,AOIP02,a")]   // mixed case is valid
  public void InvalidInputNames_Accepts_Api_Names(string csv)
  {
    GenelecSmartIpInputMatch.InvalidInputNames(csv.Split(',')).Should().BeEmpty();
  }

  [Theory]
  [InlineData(new[] { "AoIP1" }, new[] { "AoIP1" })]                       // typo
  [InlineData(new[] { "AoIP01", "Dante1" }, new[] { "Dante1" })]           // only the bad entry is returned
  [InlineData(new[] { "B", "AoIP03" }, new[] { "B", "AoIP03" })]
  [InlineData(new[] { "" }, new[] { "" })]
  [InlineData(new[] { " AoIP01 ", "AoIP02" }, new[] { " AoIP01 " })]      // padding would be sent as-is
  [InlineData(new[] { "A ", "\tAoIP02" }, new[] { "A ", "\tAoIP02" })]
  public void InvalidInputNames_Returns_Only_The_Bad_Entries(string[] inputs, string[] expected)
  {
    GenelecSmartIpInputMatch.InvalidInputNames(inputs).Should().Equal(expected);
  }

  [Fact]
  public void InvalidInputNames_Null_Entries_And_Lists()
  {
    GenelecSmartIpInputMatch.InvalidInputNames(new string?[] { "A", null }!).Should().Equal("(null)");
    GenelecSmartIpInputMatch.InvalidInputNames(null).Should().BeEmpty();
    GenelecSmartIpInputMatch.InvalidInputNames(new string[0]).Should().BeEmpty();
  }

  [Fact]
  public void ValidInputNames_Match_The_Api_Doc()
  {
    GenelecSmartIpInputMatch.ValidInputNames.Should().Equal("A", "AoIP01", "AoIP02");
  }

  [Fact]
  public void ResolveKey_Unknown_Key_Is_Rejected()
  {
    GenelecSmartIpInputMatch.ResolveKey("AoIP01", Keys, out var notFound).Should().BeNull();
    notFound.Should().BeTrue();   // an input name is not an item key
  }
}
