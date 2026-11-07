/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;

namespace Arise_RTM
{
    /// <summary>Kills, deaths et ratio K/D, comme dans le script original (kills et deaths sont écrits avec le ratio).</summary>
    public static class KdOption
    {
        private const long IntMax = int.MaxValue;
        private const long BaseDeaths = 100000000;

        public static OptionResult ApplyRatio(OptionContext context, double ratio)
        {
            int sign = ratio < 0 ? -1 : 1;
            double absRatio = Math.Abs(ratio);
            if (absRatio == 0)
                return Write(context, 0, 1);

            long deaths = BaseDeaths;
            if (absRatio * deaths > IntMax)
                deaths = Math.Max(1, (long)Math.Floor(IntMax / absRatio));

            long kills = Math.Min(IntMax, (long)Math.Floor(absRatio * deaths + 0.5)) * sign;
            return Write(context, kills, deaths);
        }

        public static OptionResult ApplyKills(OptionContext context, long kills)
        {
            kills = Math.Max(-IntMax, Math.Min(IntMax, kills));
            return Write(context, kills, CurrentValue(context, "MPPLY_DEATHS_PLAYER"));
        }

        public static OptionResult ApplyDeaths(OptionContext context, long deaths)
        {
            deaths = Math.Max(0, Math.Min(IntMax, deaths));
            return Write(context, CurrentValue(context, "MPPLY_KILLS_PLAYERS"), deaths);
        }

        private static long CurrentValue(OptionContext context, string statName)
        {
            return context.Engine.TryGetInt(statName, out int value) ? value : 0;
        }

        private static OptionResult Write(OptionContext context, long kills, long deaths)
        {
            OptionResult result = new OptionResult();
            context.WriteInt(result, "MPPLY_DEATHS_PLAYER_SUICIDE", 0);
            context.WriteInt(result, "MPPLY_KILLS_PLAYERS", (int)kills);
            context.WriteInt(result, "MPPLY_DEATHS_PLAYER", (int)deaths);

            if (kills >= 0)
                context.WriteInt(result, "MPPLY_DM_TOTAL_KILLS", (int)kills);

            context.WriteFloat(result, "MPPLY_KILL_DEATH_RATIO", deaths != 0 ? (float)((double)kills / deaths) : 0f);
            context.SaveIfRequested(result);
            context.Log($"Éliminations {kills:N0} / morts {deaths:N0}.");
            return result;
        }
    }
}
