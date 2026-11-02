using HCC.Core.Models;
using HCC.Infrastructure.Monitoring;
using Xunit;

namespace HCC.Tests;

public sealed class FanCurveEngineTests
{
    [Theory]
    [InlineData(40, 20)]
    [InlineData(60, 40)]
    [InlineData(80, 60)]
    public void EvaluatesSupportPointsAndLinearInterpolation(double temperature, double expectedPercent)
    {
        FanCurvePoint[] curve = [new(40, 20), new(80, 60)];

        Assert.True(FanCurveEngine.TryEvaluate(curve, temperature, out var percent, out var error));
        Assert.Null(error);
        Assert.Equal(expectedPercent, percent, 6);
    }

    [Theory]
    [InlineData(35, 20)]
    [InlineData(0, 20)]
    [InlineData(90, 60)]
    [InlineData(100, 60)]
    public void ClampsOutsideTheCurveRange(double temperature, double expectedPercent)
    {
        FanCurvePoint[] curve = [new(40, 20), new(80, 60)];

        Assert.True(FanCurveEngine.TryEvaluate(curve, temperature, out var percent, out _));
        Assert.Equal(expectedPercent, percent, 6);
    }

    [Fact]
    public void AcceptsMinimumAndMaximumPointCounts()
    {
        Assert.True(FanCurveEngine.TryValidate([new(30, 30), new(90, 90)], out _));

        FanCurvePoint[] eightPoints =
        [
            new(0, 20), new(10, 25), new(20, 30), new(30, 40),
            new(40, 50), new(50, 60), new(60, 70), new(70, 80)
        ];
        Assert.True(FanCurveEngine.TryValidate(eightPoints, out _));
    }

    [Fact]
    public void RejectsWrongPointCounts()
    {
        Assert.False(FanCurveEngine.TryValidate([new(30, 30)], out var tooFew));
        Assert.NotNull(tooFew);

        FanCurvePoint[] ninePoints =
        [
            new(0, 20), new(10, 25), new(20, 30), new(30, 40), new(40, 50),
            new(50, 60), new(60, 70), new(70, 80), new(80, 90)
        ];
        Assert.False(FanCurveEngine.TryValidate(ninePoints, out var tooMany));
        Assert.NotNull(tooMany);
    }

    [Fact]
    public void RejectsDuplicateOrUnsortedTemperatures()
    {
        Assert.False(FanCurveEngine.TryValidate([new(50, 30), new(50, 40)], out _));
        Assert.False(FanCurveEngine.TryValidate([new(60, 30), new(50, 40)], out _));
    }

    [Theory]
    [InlineData(double.NaN, 30)]
    [InlineData(double.PositiveInfinity, 30)]
    [InlineData(-1, 30)]
    [InlineData(101, 30)]
    [InlineData(50, double.NaN)]
    [InlineData(50, double.PositiveInfinity)]
    [InlineData(50, -1)]
    [InlineData(50, 101)]
    public void RejectsNonFiniteOrOutOfRangeValues(double temperature, double percent)
    {
        Assert.False(FanCurveEngine.TryValidate([new(30, 30), new(temperature, percent)], out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void InvalidSensorValuesNeverProduceAFanValue(double temperature)
    {
        FanCurvePoint[] curve = [new(40, 20), new(80, 60)];

        Assert.False(FanCurveEngine.TryEvaluate(curve, temperature, out var percent, out var error));
        Assert.Equal(0, percent);
        Assert.NotNull(error);
    }
}
