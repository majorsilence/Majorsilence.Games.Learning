using Majorsilence.Games.Learning;
using Xunit;

namespace Majorsilence.Games.Core.Tests;

/// <summary>
/// The flight of a coin fired from the tix launcher. Pure math - the sprite
/// sheet is only ever touched when drawing, so a coin can be flown without SDL.
///
/// What these pin is the ground contract: a coin comes to rest on whatever it
/// is over, at the height that thing sits at. It used to stop at zero whatever
/// it had flown across, so a coin thrown onto a raised terrace settled inside
/// the deck rather than on it.
/// </summary>
public class LaunchedTixTest
{
    /// <summary>A coin thrown straight up, so only the vertical behaviour is in play.</summary>
    private static LaunchedTix Coin(float upward = 200f) => new(null!, 0f, 0f, upward);

    /// <summary>Flies the coin at a fixed 120Hz until it settles, or gives up.</summary>
    private static int Fly(LaunchedTix coin, int maxSteps = 2000)
    {
        var steps = 0;
        while (!coin.Landed && steps < maxSteps)
        {
            coin.Update(1f / 120f);
            steps++;
        }

        return steps;
    }

    [Fact]
    public void OnFlatGroundACoinSettlesAtZero()
    {
        var coin = Coin();

        Fly(coin);

        Assert.True(coin.Landed);
        Assert.Equal(0f, coin.Z, 3);
    }

    [Fact]
    public void ACoinComesToRestOnTheTerraceItLandsOn()
    {
        var coin = Coin();
        coin.GroundZ = 32f; // two elevation steps up

        Fly(coin);

        Assert.True(coin.Landed);
        Assert.Equal(32f, coin.Z, 3);
    }

    [Fact]
    public void HigherGroundIsReachedSooner()
    {
        var low = Coin();
        var high = Coin();
        high.GroundZ = 32f;

        // Same throw, less distance to fall back down.
        Assert.True(Fly(high) < Fly(low));
    }

    /// <summary>
    /// GroundZ is supplied from outside every frame, so it changes underneath a
    /// coin in flight as it crosses tiles. Whatever it is over when it comes
    /// down is what it lands on.
    /// </summary>
    [Fact]
    public void TheGroundItLandsOnIsTheOneItIsOverAtTheTime()
    {
        var coin = Coin();

        // Flies out over flat deck, then crosses onto a terrace on the way down.
        for (var i = 0; i < 30; i++) coin.Update(1f / 120f);
        Assert.False(coin.Landed);
        coin.GroundZ = 16f;

        Fly(coin);

        Assert.Equal(16f, coin.Z, 3);
    }

    [Fact]
    public void ALandedCoinStaysPut()
    {
        var coin = Coin();
        coin.GroundZ = 16f;
        Fly(coin);

        var restingZ = coin.Z;
        var restingX = coin.X;
        for (var i = 0; i < 60; i++) coin.Update(1f / 120f);

        Assert.Equal(restingZ, coin.Z, 3);
        Assert.Equal(restingX, coin.X);
    }

    [Fact]
    public void ACoinLaunchedSidewaysTravelsBeforeItSettles()
    {
        var coin = new LaunchedTix(null!, velocityX: 60f, velocityY: 20f, velocityZ: 200f);

        Fly(coin);

        Assert.True(coin.Landed);
        Assert.True(coin.X > 0, "a coin thrown sideways should come down somewhere else");
    }
}
