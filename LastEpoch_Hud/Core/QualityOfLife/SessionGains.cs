using System;

namespace LastEpoch_Hud.Scripts.Core.QualityOfLife;

public enum GainCurrency
{
    Experience,
    Favour,
    MemoryAmber,
}

// Gross positive gains, averaged over active session time. Spending is not a gain.
public sealed class SessionGains
{
    readonly long[] totals = new long[3];
    public double ActiveSeconds { get; private set; }
    public bool Paused { get; set; }

    public long Total(GainCurrency currency) => totals[(int)currency];

    public double PerHour(GainCurrency currency) =>
        ActiveSeconds > 0 ? Total(currency) * (3600d / ActiveSeconds) : 0;

    public void Advance(double seconds, bool active)
    {
        if (active && !Paused && double.IsFinite(seconds) && seconds > 0)
            ActiveSeconds = Math.Min(double.MaxValue, ActiveSeconds + seconds);
    }

    public void RecordIncrease(GainCurrency currency, long before, long after)
    {
        if (Paused || before < 0 || after <= before)
            return;
        long gain = after - before;
        int index = (int)currency;
        totals[index] = gain > long.MaxValue - totals[index] ? long.MaxValue : totals[index] + gain;
    }

    public void Reset()
    {
        Array.Clear(totals, 0, totals.Length);
        ActiveSeconds = 0;
        Paused = false;
    }
}
