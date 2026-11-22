using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Step order of one custom unique registration.</summary>
public sealed class CustomUniqueProgress
{
    private CustomUniqueStep _step;

    public CustomUniqueProgress(bool addsBase)
    {
        _step = addsBase ? CustomUniqueStep.AddBase : CustomUniqueStep.AddUnique;
    }

    public bool IsFinished => _step == CustomUniqueStep.Done || _step == CustomUniqueStep.Failed;

    public CustomUniqueStep Next(bool gameReady)
    {
        if (IsFinished)
        {
            return _step;
        }

        return gameReady ? _step : CustomUniqueStep.Wait;
    }

    /// <summary>Runs pending steps in order until waiting, finished, or a step leaves the step unchanged.</summary>
    public void RunPending(bool gameReady, Action<CustomUniqueStep> run)
    {
        CustomUniqueStep step = Next(gameReady);
        while (IsRunnable(step))
        {
            run(step);
            if (Next(true) == step)
            {
                return;
            }

            step = Next(true);
        }
    }

    public void Complete()
    {
        if (IsFinished)
        {
            return;
        }

        _step++;
    }

    public void Fail()
    {
        if (_step == CustomUniqueStep.Done)
        {
            return;
        }

        _step = CustomUniqueStep.Failed;
    }

    private static bool IsRunnable(CustomUniqueStep step)
    {
        return step != CustomUniqueStep.Wait
            && step != CustomUniqueStep.Done
            && step != CustomUniqueStep.Failed;
    }
}
