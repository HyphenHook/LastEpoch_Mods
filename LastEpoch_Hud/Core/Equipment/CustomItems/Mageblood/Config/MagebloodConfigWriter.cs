using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>Turns a config into indented JSON text.</summary>
public static class MagebloodConfigWriter
{
    public static string Write(MagebloodConfig config)
    {
        var flasks = new JArray();
        foreach (MagebloodFlaskEntry flask in config.Flasks)
        {
            flasks.Add(BuildFlask(flask));
        }

        var root = new JObject
        {
            [MagebloodConfigKeys.Version] = config.Version,
            [MagebloodConfigKeys.DefaultsVersion] = MagebloodConfigDefaults.DefaultsVersion,
            [MagebloodConfigKeys.MaxResistances] = config.MaxResistances,
            [MagebloodConfigKeys.Flasks] = flasks,
        };
        return root.ToString(Formatting.Indented);
    }

    internal static JObject BuildFlask(MagebloodFlaskEntry flask)
    {
        var obj = new JObject { [MagebloodConfigKeys.Name] = flask.Name };
        if (flask.Icon != null)
        {
            obj[MagebloodConfigKeys.Icon] = flask.Icon;
        }

        var rows = new JArray();
        foreach (MagebloodStatEntry row in flask.Stats)
        {
            rows.Add(BuildRow(row));
        }
        obj[MagebloodConfigKeys.Stats] = rows;
        return obj;
    }

    private static JObject BuildRow(MagebloodStatEntry row)
    {
        var obj = new JObject { [MagebloodConfigKeys.Stat] = row.Stat };
        if (row.Tag != null)
        {
            obj[MagebloodConfigKeys.Tag] = row.Tag;
        }

        obj[MagebloodConfigKeys.Added] = row.Added;
        obj[MagebloodConfigKeys.Increased] = row.Increased;
        if (row.More != 0f)
        {
            obj[MagebloodConfigKeys.More] = row.More;
        }
        return obj;
    }
}
