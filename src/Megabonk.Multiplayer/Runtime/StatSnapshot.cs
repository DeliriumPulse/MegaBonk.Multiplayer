using System;
using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Menu.Shop;

namespace Megabonk.Multiplayer
{
    public sealed class StatSnapshot
    {
        public static readonly StatSnapshot Empty = new StatSnapshot(new Dictionary<EStat, float>(0));

        public Dictionary<EStat, float> Values { get; }

        public StatSnapshot(Dictionary<EStat, float> values)
        {
            Values = values ?? new Dictionary<EStat, float>(0);
        }

        public bool TryGet(EStat stat, out float value)
        {
            return Values.TryGetValue(stat, out value);
        }

        public bool IsEmpty => Values.Count == 0;

        public StatSnapshot Clone()
        {
            if (Values.Count == 0)
                return Empty;
            return new StatSnapshot(new Dictionary<EStat, float>(Values));
        }

        public static string Serialize(StatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Values.Count == 0)
                return string.Empty;

            var parts = new List<string>(snapshot.Values.Count);
            foreach (var kvp in snapshot.Values)
            {
                var valueString = kvp.Value.ToString("R", CultureInfo.InvariantCulture);
                parts.Add($"{(int)kvp.Key}={valueString}");
            }
            return string.Join(";", parts);
        }

        public static StatSnapshot Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return Empty;

            var dict = new Dictionary<EStat, float>();
            var entries = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                int eqIndex = entry.IndexOf('=');
                if (eqIndex <= 0 || eqIndex >= entry.Length - 1)
                    continue;

                var keyPart = entry.Substring(0, eqIndex);
                var valuePart = entry.Substring(eqIndex + 1);

                if (!int.TryParse(keyPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyInt))
                    continue;

                if (!float.TryParse(valuePart, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
                    continue;

                dict[(EStat)keyInt] = value;
            }

            if (dict.Count == 0)
                return Empty;

            return new StatSnapshot(dict);
        }
    }

    internal static class StatSnapshotBuilder
    {
        public static bool TryCapture(out StatSnapshot snapshot)
        {
            snapshot = StatSnapshot.Empty;
            try
            {
                if (!Assets.Scripts.Inventory.Stats.PlayerStats.HasStats())
                    return false;
            }
            catch
            {
                return false;
            }

            var values = new Dictionary<EStat, float>();
            var enumValues = (EStat[])Enum.GetValues(typeof(EStat));
            for (int i = 0; i < enumValues.Length; i++)
            {
                var stat = enumValues[i];
                try
                {
                    float value = Assets.Scripts.Inventory.Stats.PlayerStats.GetStat(stat);
                    values[stat] = value;
                }
                catch
                {
                    // swallow individual stat failures
                }
            }

            snapshot = values.Count > 0 ? new StatSnapshot(values) : StatSnapshot.Empty;
            return !snapshot.IsEmpty;
        }
    }
}
