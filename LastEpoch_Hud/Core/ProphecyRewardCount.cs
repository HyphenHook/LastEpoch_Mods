using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core;

// A synchronous, temporary override. Nested triggers sharing one reward asset
// inherit the outer count instead of applying the multiplier again.
internal sealed class ProphecyRewardCount : IDisposable
{
    [ThreadStatic]
    private static HashSet<long> activeRewards;

    private readonly long key;
    private readonly int originalCount;
    private readonly Action<int> writeCount;
    private bool disposed;

    private ProphecyRewardCount(long key, int originalCount, Action<int> writeCount)
    {
        this.key = key;
        this.originalCount = originalCount;
        this.writeCount = writeCount;
    }

    internal static int Factor(float value) =>
        float.IsFinite(value) ? Math.Clamp((int)Math.Clamp(MathF.Round(value), 1, 10), 1, 10) : 1;

    internal static ProphecyRewardCount Begin(
        long key,
        int count,
        float multiplier,
        Action<int> writeCount
    )
    {
        int factor = Factor(multiplier);
        long multiplied = (long)count * factor;
        if (key == 0 || count <= 0 || factor <= 1 || multiplied > int.MaxValue)
            return null;

        activeRewards ??= new HashSet<long>();
        if (!activeRewards.Add(key))
            return null;

        var scope = new ProphecyRewardCount(key, count, writeCount);
        try
        {
            writeCount((int)multiplied);
            return scope;
        }
        catch
        {
            // A setter may have written before reporting a failure.
            scope.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            writeCount(originalCount);
        }
        finally
        {
            activeRewards.Remove(key);
        }
    }
}
