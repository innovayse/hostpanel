namespace Innovayse.Application.Tests.Notifications;

using Innovayse.Application.Notifications.Services;
using Xunit;

/// <summary>
/// Tests for <see cref="BrandPalette"/>. The expected values are the same ones
/// <c>client/utils/brandPalette.test.ts</c> asserts, so the two ports cannot drift apart
/// without a red build on at least one side.
/// </summary>
public class BrandPaletteTests
{
    [Theory]
    [InlineData("#0ea5e9", 500, "#0ea5e9")]
    [InlineData("#0ea5e9", 50, "#f5fbff")]
    [InlineData("#0ea5e9", 700, "#145e84")]
    [InlineData("#0ea5e9", 950, "#000d19")]
    [InlineData("#1a73e8", 700, "#154485")]
    [InlineData("#ffd600", 700, "#8d7710")]
    [InlineData("#a855f7", 200, "#e6d5ff")]
    public void Shade_MatchesTheTypeScriptPort(string hex, int step, string expected)
    {
        Assert.Equal(expected, BrandPalette.Shade(hex, step));
    }

    [Fact]
    public void Shade_PinsThePickedColourAt500_WhateverTheCase()
    {
        Assert.Equal("#1a73e8", BrandPalette.Shade("#1A73E8", 500));
    }

    [Fact]
    public void Shade_RefusesANonColourAndAnUnknownStep()
    {
        Assert.Throws<ArgumentException>(() => BrandPalette.Shade("blue", 500));
        Assert.Throws<ArgumentException>(() => BrandPalette.Shade("#0ea5e9", 550));
    }

    [Fact]
    public void ContrastRatio_MatchesTheWcagReferenceNumbers()
    {
        Assert.Equal(21, BrandPalette.ContrastRatio("#ffffff", "#000000"), tolerance: 0.001);
        Assert.Equal(2.78, BrandPalette.ContrastRatio("#ffffff", "#0ea5e9"), tolerance: 0.05);
        Assert.Equal(4.5, BrandPalette.ContrastRatio("#ffffff", "#1a73e8"), tolerance: 0.5);
        Assert.True(double.IsNaN(BrandPalette.ContrastRatio("blue", "#ffffff")));
    }

    [Theory]
    [InlineData("#1a73e8", BrandPalette.White)]
    [InlineData("#a855f7", BrandPalette.White)]
    [InlineData("#ffd600", BrandPalette.Ink)]
    [InlineData("#7de3ff", BrandPalette.Ink)]
    public void BestTextOn_PutsWhiteOnBluesAndInkOnYellows(string surface, string expected)
    {
        Assert.Equal(expected, BrandPalette.BestTextOn(surface));
    }
}
