/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

namespace Arise_RTM
{
    /// <summary>Un lieu de téléport nommé, chargé depuis teleports.txt.</summary>
    public sealed class TeleportLocation
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public TeleportLocation(string name, float x, float y, float z)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => Name;
    }
}
