/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System;
using System.Globalization;
using System.Threading;

namespace Arise_RTM
{
    /// <summary>
    /// Téléporte le joueur à des coordonnées arbitraires en écrivant la position en mémoire
    /// (les deux copies que le jeu conserve). Aucune fonction native n'est appelée. L'outil
    /// vérifie d'abord que les deux copies concordent, écrit, puis relit après une courte pause
    /// pour confirmer que le jeu n'a pas annulé le déplacement (protection anti-téléportation).
    /// <see cref="ClothingShopTeleportOption"/> réutilise cette routine avec les coordonnées
    /// de la boutique.
    /// </summary>
    public static class TeleportOption
    {
        private const float FeetClearance = 1f;
        private const float MaxCopyDifference = 0.5f;
        private const float ConfirmationTolerance = 2f;
        private const int ConfirmationDelayMs = 200;

        public static readonly string[] RequiredHandlerKeys = new string[0];
        public static readonly string[] OptionalHandlerKeys = new string[0];

        /// <summary>Téléporte vers un lieu nommé.</summary>
        public static OptionResult ApplyTo(OptionContext context, TeleportLocation location)
        {
            if (location == null)
                return new OptionResult { Skipped = 1, Note = "Aucun lieu sélectionné." };

            return ApplyTo(context, location.X, location.Y, location.Z, location.Name);
        }

        /// <summary>Téléporte vers des coordonnées libres (rajoute +1 Z pour éviter le sol).</summary>
        public static OptionResult ApplyTo(OptionContext context, float x, float y, float z, string label = null)
        {
            StatEngine engine = context.Engine;

            if (!PlayerPedMemory.TryLocate(engine, out PlayerPedMemory.Location ped))
                return Stopped("Ped du joueur introuvable — soyez en session avec votre personnage chargé.");

            context.Log?.Invoke($"Ped à 0x{ped.Ped:X}, transform à 0x{ped.Transform:X}.");

            if (!PlayerPedMemory.TryReadPosition(engine, ped.TransformPosition, out float tx, out float ty, out float tz) ||
                !PlayerPedMemory.TryReadPosition(engine, ped.PedPosition, out float px, out float py, out float pz))
                return Stopped("Position courante illisible — rien n'a été écrit.");

            context.Log?.Invoke(Format("Position actuelle", tx, ty, tz) + " / " + Format("copie", px, py, pz));

            if (Math.Abs(tx - px) > MaxCopyDifference ||
                Math.Abs(ty - py) > MaxCopyDifference ||
                Math.Abs(tz - pz) > MaxCopyDifference)
            {
                return Stopped("Les deux copies de position diffèrent (layout mémoire différent sur ce build) — rien n'a été écrit.");
            }

            float finalZ = z + FeetClearance;
            PlayerPedMemory.WritePosition(engine, ped.TransformPosition, x, y, finalZ);
            PlayerPedMemory.WritePosition(engine, ped.PedPosition, x, y, finalZ);

            // Laisse au jeu le temps d'appliquer ou d'annuler le déplacement avant de relire.
            Thread.Sleep(ConfirmationDelayMs);

            bool transformRead = PlayerPedMemory.TryReadPosition(engine, ped.TransformPosition, out float rx, out float ry, out float rz);
            bool pedRead = PlayerPedMemory.TryReadPosition(engine, ped.PedPosition, out float cx, out float cy, out float cz);

            if (transformRead && pedRead)
                context.Log?.Invoke(Format("Relecture", rx, ry, rz) + " / " + Format("copie", cx, cy, cz));
            else if (transformRead)
                context.Log?.Invoke(Format("Relecture", rx, ry, rz) + " / copie illisible");
            else
                context.Log?.Invoke("Relecture impossible après écriture.");

            bool confirmed = transformRead && pedRead && Near(rx, ry, rz, x, y, finalZ) && Near(cx, cy, cz, x, y, finalZ);
            if (!confirmed)
            {
                string actual = transformRead ? Format(null, rx, ry, rz) : "illisible";
                return new OptionResult
                {
                    Mismatched = 1,
                    Note = $"Téléportation non confirmée : position relue {actual} au lieu de {Format(null, x, y, finalZ)}. " +
                           "Vérifie qu'aucune protection anti-téléportation n'est active."
                };
            }

            string note = label != null
                ? $"Téléporté à : {label} ({Format(null, x, y, finalZ)})."
                : $"Téléporté à {Format(null, x, y, finalZ)}.";

            return new OptionResult { Written = 1, Note = note };
        }

        private static bool Near(float ax, float ay, float az, float bx, float by, float bz)
        {
            return Math.Abs(ax - bx) <= ConfirmationTolerance &&
                   Math.Abs(ay - by) <= ConfirmationTolerance &&
                   Math.Abs(az - bz) <= ConfirmationTolerance;
        }

        private static OptionResult Stopped(string note)
        {
            return new OptionResult { Skipped = 1, Note = note };
        }

        private static string Format(string label, float x, float y, float z)
        {
            string coords = string.Format(CultureInfo.InvariantCulture, "{0:0.00}, {1:0.00}, {2:0.00}", x, y, z);
            return label == null ? coords : label + " : " + coords;
        }
    }
}
