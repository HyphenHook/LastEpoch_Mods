namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Keeps the hidden area buff in step with the model scale.</summary>
public sealed class HeadhunterAreaMatch
{
    public const string BuffName = "HH_ModelSizeArea";
    public const float DurationSeconds = 3600f;

    private readonly int _statId;
    private float _applied;

    public HeadhunterAreaMatch(int statId)
    {
        _statId = statId;
    }

    /// <summary>More-area fraction for a model scale (area grows with the square of the radius).</summary>
    public static float MoreArea(float scale)
    {
        return scale <= 1f ? 0f : (scale * scale) - 1f;
    }

    /// <summary>The buff change for this refresh. Remembers the applied bonus.</summary>
    public bool TryNext(float scale, bool buffLive, out BuffAction action)
    {
        float bonus = MoreArea(scale);
        action = default;
        if (bonus <= 0f)
        {
            return TryRemove(buffLive, out action);
        }
        if (buffLive && bonus == _applied)
        {
            return false;
        }

        _applied = bonus;
        action = new BuffAction(
            BuffActionKind.Add,
            BuffName,
            _statId,
            0f,
            0f,
            DurationSeconds,
            1,
            More: bonus
        );
        return true;
    }

    /// <summary>Forgets the bonus and returns the remove action.</summary>
    public BuffAction Clear()
    {
        _applied = 0f;
        return RemoveAction();
    }

    private bool TryRemove(bool buffLive, out BuffAction action)
    {
        _applied = 0f;
        action = RemoveAction();
        return buffLive;
    }

    private BuffAction RemoveAction()
    {
        return new BuffAction(BuffActionKind.Remove, BuffName, _statId, 0f, 0f, 0f, 0);
    }
}
