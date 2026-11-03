using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Tests;

public class VolumeScalingTests
{
  [Theory]
  [InlineData(-130.0, 0)]
  [InlineData(0.0, 65535)]
  [InlineData(-65.0, 32768)]
  [InlineData(-200.0, 0)]   // below range clamps
  [InlineData(10.0, 65535)] // above range clamps
  public void ToScaled_Maps_Linearly_And_Clamps(double db, int expected)
  {
    GenelecSmartIpVolumeScaling.ToScaled(db, -130, 0).Should().Be((ushort)expected);
  }

  [Fact]
  public void ToDb_Endpoints()
  {
    GenelecSmartIpVolumeScaling.ToDb(0, -130, 0).Should().Be(-130);
    GenelecSmartIpVolumeScaling.ToDb(65535, -130, 0).Should().Be(0);
  }

  [Fact]
  public void ToDb_Rounds_To_Tenth_Db()
  {
    var db = GenelecSmartIpVolumeScaling.ToDb(12345, -130, 0);
    (db * 10).Should().BeApproximately(Math.Round(db * 10), 1e-9);
  }

  [Fact]
  public void Round_Trip_Is_Within_One_Tenth_Db()
  {
    for (ushort s = 0; s < 65535; s += 997)
    {
      var db = GenelecSmartIpVolumeScaling.ToDb(s, -130, 0);
      var back = GenelecSmartIpVolumeScaling.ToScaled(db, -130, 0);
      var dbBack = GenelecSmartIpVolumeScaling.ToDb(back, -130, 0);
      Math.Abs(dbBack - db).Should().BeLessOrEqualTo(0.1);
    }
  }

  [Fact]
  public void Custom_Max_Cap_Maps_Full_Scale_To_Cap()
  {
    GenelecSmartIpVolumeScaling.ToDb(65535, -130, -10).Should().Be(-10);
  }

  [Fact]
  public void Degenerate_Range_Returns_Zero()
  {
    GenelecSmartIpVolumeScaling.ToScaled(-5, 0, 0).Should().Be(0);
  }

  [Theory]
  [InlineData(-140.0, -130.0)]
  [InlineData(5.0, 0.0)]
  [InlineData(-12.34, -12.3)]
  public void Clamp_Limits_And_Rounds(double input, double expected)
  {
    GenelecSmartIpVolumeScaling.Clamp(input, -130, 0).Should().Be(expected);
  }
}
