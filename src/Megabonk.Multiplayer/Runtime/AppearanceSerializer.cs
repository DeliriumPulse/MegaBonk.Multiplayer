using System;
using System.Text;

namespace Megabonk.Multiplayer
{
    internal static class AppearanceSerializer
    {
        private const char FieldSeparator = '|';
        private const char MaterialSeparator = ',';

        public static string Serialize(in AppearanceInfo info)
        {
            var fields = new string[8];
            fields[0] = Encode(info.RootPath);
            fields[1] = Encode(info.PrefabName);
            fields[2] = Encode(info.MeshName);
            fields[3] = EncodeMaterials(info.MaterialNames);
            fields[4] = Encode(info.CharacterClass);
            fields[5] = EncodeInt(info.CharacterId);
            fields[6] = Encode(info.SkinName);
            fields[7] = Encode(StatSnapshot.Serialize(info.Stats ?? StatSnapshot.Empty));
            return string.Join(FieldSeparator, fields);
        }

        public static bool TryDeserialize(string payload, out AppearanceInfo info)
        {
            info = default;
            if (string.IsNullOrEmpty(payload))
                return false;

            var parts = payload.Split(FieldSeparator);
            if (parts.Length < 7)
            {
                var expanded = new string[8];
                for (int i = 0; i < parts.Length; i++)
                    expanded[i] = parts[i];
                parts = expanded;
            }

            info = new AppearanceInfo
            {
                RootPath = Decode(parts[0]),
                PrefabName = Decode(parts[1]),
                MeshName = Decode(parts[2]),
                MaterialNames = DecodeMaterials(parts.Length > 3 ? parts[3] : string.Empty),
                CharacterClass = Decode(parts.Length > 4 ? parts[4] : string.Empty),
                CharacterId = parts.Length > 5 ? DecodeInt(parts[5]) : -1,
                SkinName = Decode(parts.Length > 6 ? parts[6] : string.Empty),
                Stats = StatSnapshot.Deserialize(Decode(parts.Length > 7 ? parts[7] : string.Empty))
            };

            return true;
        }

        private static string Encode(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        private static string EncodeInt(int value)
        {
            return Encode(value.ToString());
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                var data = Convert.FromBase64String(value);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int DecodeInt(string value)
        {
            var decoded = Decode(value);
            if (int.TryParse(decoded, out var result))
                return result;
            return -1;
        }

        private static string EncodeMaterials(string[] materials)
        {
            if (materials == null || materials.Length == 0)
                return string.Empty;

            var encoded = new string[materials.Length];
            for (int i = 0; i < materials.Length; i++)
                encoded[i] = Encode(materials[i]);
            return string.Join(MaterialSeparator, encoded);
        }

        private static string[] DecodeMaterials(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return Array.Empty<string>();

            var parts = payload.Split(MaterialSeparator);
            var decoded = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                decoded[i] = Decode(parts[i]);
            return decoded;
        }
    }
}
