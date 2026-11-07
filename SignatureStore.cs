/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace Arise_RTM
{
    public sealed class SignatureStore
    {
        private readonly string path;
        private readonly Dictionary<string, Dictionary<string, string>> sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public SignatureStore(string path)
        {
            this.path = path;
            Load();
        }

        public bool TryGet(string section, string key, out byte[] bytes)
        {
            bytes = null;
            if (!sections.TryGetValue(section, out Dictionary<string, string> values) ||
                !values.TryGetValue(key, out string hex))
                return false;

            bytes = FromHex(hex);
            return bytes.Length > 0;
        }

        public void Save(string section, string key, byte[] bytes)
        {
            if (!sections.TryGetValue(section, out Dictionary<string, string> values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[section] = values;
            }

            values[key] = BitConverter.ToString(bytes).Replace("-", string.Empty);
            Write();
        }

        private void Load()
        {
            if (!File.Exists(path))
                return;

            Dictionary<string, string> current = null;
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("["))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[line.Trim('[', ']').Trim()] = current;
                    continue;
                }

                int separator = line.IndexOf('=');
                if (current != null && separator > 0)
                    current[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
        }

        private void Write()
        {
            List<string> lines = new List<string> { "# Premiers octets de chaque fonction du jeu, appris lors des exécutions vérifiées." };
            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                lines.Add(string.Empty);
                lines.Add("[" + section.Key + "]");
                foreach (KeyValuePair<string, string> entry in section.Value)
                    lines.Add(entry.Key + " = " + entry.Value);
            }

            File.WriteAllLines(path, lines);
        }

        private static byte[] FromHex(string hex)
        {
            if (hex.Length % 2 != 0)
                return new byte[0];

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    return new byte[0];
            }

            return bytes;
        }
    }
}
