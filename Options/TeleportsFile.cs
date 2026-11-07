/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arise_RTM
{
    /// <summary>
    /// Gestionnaire du fichier teleports.txt (lecture, écriture, CRUD).
    /// Au premier lancement, le fichier est généré automatiquement avec une base
    /// de lieux vérifiés que l'utilisateur peut ensuite modifier à volonté.
    /// Aucune liste n'est en dur dans le reste du code : tout passe par ce fichier.
    /// </summary>
    public sealed class TeleportsFile
    {
        private const string DefaultFileName = "teleports.txt";
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        public string FilePath { get; }
        public List<TeleportLocation> Entries { get; private set; } = new List<TeleportLocation>();

        public TeleportsFile(string directory)
        {
            FilePath = Path.Combine(directory, DefaultFileName);
        }

        /// <summary>Charge le fichier. S'il n'existe pas, le crée avec les lieux par défaut.</summary>
        public void LoadOrSeed()
        {
            if (!File.Exists(FilePath))
            {
                Entries = DefaultSeed();
                Save();
                return;
            }

            Entries = Parse(File.ReadAllLines(FilePath, Encoding.UTF8));
        }

        /// <summary>Ajoute un lieu. Renvoie false si le nom existe déjà (doublon refusé).</summary>
        public bool Add(string name, float x, float y, float z)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (IndexOf(name) >= 0) return false;
            Entries.Add(new TeleportLocation(name.Trim(), x, y, z));
            Save();
            return true;
        }

        /// <summary>Met à jour les coords d'un lieu existant (par nom).</summary>
        public bool Update(string name, float x, float y, float z)
        {
            int idx = IndexOf(name);
            if (idx < 0) return false;
            Entries[idx].X = x;
            Entries[idx].Y = y;
            Entries[idx].Z = z;
            Save();
            return true;
        }

        /// <summary>Renomme un lieu existant (et met à jour ses coords au passage).</summary>
        public bool Rename(string oldName, string newName, float x, float y, float z)
        {
            int idx = IndexOf(oldName);
            if (idx < 0) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;

            // Si le nouveau nom existe déjà pour un autre lieu, refuser
            if (!NameComparer.Equals(oldName, newName))
            {
                int collision = IndexOf(newName);
                if (collision >= 0) return false;
            }

            Entries[idx].Name = newName.Trim();
            Entries[idx].X = x;
            Entries[idx].Y = y;
            Entries[idx].Z = z;
            Save();
            return true;
        }

        /// <summary>Supprime un lieu par nom.</summary>
        public bool Remove(string name)
        {
            int idx = IndexOf(name);
            if (idx < 0) return false;
            Entries.RemoveAt(idx);
            Save();
            return true;
        }

        public int IndexOf(string name)
        {
            if (name == null) return -1;
            for (int i = 0; i < Entries.Count; i++)
                if (NameComparer.Equals(Entries[i].Name, name.Trim()))
                    return i;
            return -1;
        }

        // ---- IO ------------------------------------------------------------------------

        private static List<TeleportLocation> Parse(string[] lines)
        {
            List<TeleportLocation> list = new List<TeleportLocation>();
            foreach (string raw in lines)
            {
                string line = raw?.Trim() ?? string.Empty;
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] parts = line.Split('|');
                if (parts.Length != 4) continue;

                string name = parts[0].Trim();
                if (name.Length == 0) continue;
                if (!TryFloat(parts[1], out float x)) continue;
                if (!TryFloat(parts[2], out float y)) continue;
                if (!TryFloat(parts[3], out float z)) continue;

                list.Add(new TeleportLocation(name, x, y, z));
            }
            return list;
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# teleports.txt — lieux de téléport pour Arise - RTM");
            sb.AppendLine("# Un lieu par ligne. Format : Nom | X | Y | Z");
            sb.AppendLine("# Les lignes vides et les lignes commençant par # sont ignorées.");
            sb.AppendLine("# Vous pouvez modifier ce fichier à la main, l'outil le rechargera via « Rafraîchir ».");
            sb.AppendLine();

            foreach (TeleportLocation e in Entries)
            {
                sb.Append(e.Name).Append(" | ")
                  .Append(e.X.ToString("0.0000", CultureInfo.InvariantCulture)).Append(" | ")
                  .Append(e.Y.ToString("0.0000", CultureInfo.InvariantCulture)).Append(" | ")
                  .Append(e.Z.ToString("0.0000", CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }

        private static bool TryFloat(string s, out float v)
        {
            return float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>
        /// Lieux de base écrits une seule fois au premier lancement si teleports.txt est absent.
        /// Source : communauté GTA V (gta5.photography, altv.mp, gta.fandom.com), coordonnées
        /// extérieures vérifiées. L'utilisateur peut tout modifier via l'UI.
        /// </summary>
        private static List<TeleportLocation> DefaultSeed()
        {
            return new List<TeleportLocation>
            {
                new TeleportLocation("Legion Square (centre-ville)",   195.00f,    -934.00f,     30.68f),
                new TeleportLocation("Little Seoul",                  -889.655f,   -853.499f,    20.566f),
                new TeleportLocation("Mirror Park",                   1070.206f,   -711.958f,    58.483f),
                new TeleportLocation("Vinewood Sign",                  711.362f,   1198.134f,   348.526f),
                new TeleportLocation("Observatoire Galileo (toit)",   -438.804f,   1076.097f,   352.411f),
                new TeleportLocation("Maze Bank Tower (toit)",         -75.015f,   -818.215f,   326.176f),
                new TeleportLocation("Casino Diamond (porte)",         924.369f,     47.037f,    81.093f),
                new TeleportLocation("Los Santos Customs (La Mesa)",  -365.425f,   -131.809f,    37.873f),
                new TeleportLocation("Mont Chiliad (sommet)",          450.718f,   5566.614f,   806.183f),
                new TeleportLocation("Mont Gordo (sommet)",           2877.633f,   5911.078f,   369.624f),
                new TeleportLocation("Jetée de Del Perro",           -1850.127f,  -1231.751f,    13.017f),
                new TeleportLocation("Jetée de Paleto Bay",           -275.522f,   6635.835f,     7.425f),
                new TeleportLocation("Skatepark plage Vespucci",    -1374.881f,  -1398.835f,     6.141f),
                new TeleportLocation("Pillbox Hospital",               307.168f,   -590.807f,    43.280f),
                new TeleportLocation("Golf Club Los Santos",         -1336.715f,     59.051f,    55.246f),
                new TeleportLocation("Richman Hotel",                -1330.911f,    340.871f,    64.078f)
            };
        }
    }
}
