/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System;

namespace Arise_RTM
{
    /// <summary>
    /// Fixe le rang (niveau) du personnage choisi, ou directement le total de RP.
    ///
    /// Important pour GTA V Online 1.57 PS4 :
    ///   - La seule stat réellement présente dans ce build pour le niveau est MP{n}_CHAR_XP_FM
    ///     (les RP accumulés du personnage). Le rang affiché est dérivé par le jeu depuis cette
    ///     valeur — donc écrire le bon XP = le rang se met à jour automatiquement.
    ///   - Les stats classiques CHAR_FM_RANK / MPPLY_CHAR_CURRENT_RANK / MPPLY_CURRENT_RANK /
    ///     CHAR_LAST_FM_XP_LOSS ont été testées sur eboot 1.57 EU : toutes retournent
    ///     « no such stat in this game build ». On ne les écrit donc pas.
    ///
    /// Table rang → RP cumulés : valeurs officielles du jeu (source gta.wiki) pour les rangs
    /// 1 à 100, puis formule quadratique officielle f(x) = 25x² + 23575x − 1023150 pour les
    /// rangs 101 à 8000. Rang 8000 ≈ 1,79 milliard de RP.
    /// </summary>
    public static class LevelOption
    {
        public const int MinRank = 1;
        public const int MaxRank = 8000;
        public const long MaxRp = (long)int.MaxValue;

        public static readonly string[] HandlerKeysUsed = { HandlerKeys.StatSetInt };

        /// <summary>RP cumulés à l'entrée du rang N (index 0 = rang 1). Valeurs officielles.</summary>
        private static readonly long[] RpByRank =
        {
                   0,      800,     2100,     3800,     6100,     9500,    12500,    16000,    19800,    24000, //  1..10
               28500,    33400,    38700,    44200,    50200,    56400,    63000,    69900,    77100,    84700, // 11..20
               92500,   100700,   109200,   118000,   127100,   136500,   146200,   156200,   166500,   177100, // 21..30
              188000,   199200,   210700,   222400,   234500,   246800,   259400,   272300,   285500,   299000, // 31..40
              312700,   326800,   341000,   355600,   370500,   385600,   401000,   416600,   432600,   448800, // 41..50
              465200,   482000,   499000,   516300,   533800,   551600,   569600,   588000,   606500,   625400, // 51..60
              644500,   663800,   683400,   703300,   723400,   743800,   764500,   785400,   806500,   827900, // 61..70
              849600,   871500,   893600,   916000,   938700,   961600,   984700,  1008100,  1031800,  1055700, // 71..80
             1079800,  1104200,  1128800,  1153700,  1178800,  1204200,  1229800,  1255600,  1281700,  1308100, // 81..90
             1334600,  1361400,  1388500,  1415800,  1443300,  1471100,  1499100,  1527300,  1555800,  1584350  // 91..100
        };

        /// <summary>Formule quadratique officielle pour les rangs 101+ : f(x) = 25x² + 23575x − 1023150.</summary>
        private static long RpFromFormula(int rank)
        {
            return 25L * rank * rank + 23575L * rank - 1023150L;
        }

        /// <summary>RP cumulés à atteindre pour afficher le rang demandé.</summary>
        public static long RpForRank(int rank)
        {
            if (rank <= 1) return 0;
            if (rank <= 100) return RpByRank[rank - 1];
            long rp = RpFromFormula(rank);
            if (rp < 0) return 0;
            if (rp > MaxRp) return MaxRp;
            return rp;
        }

        /// <summary>Inverse : quel rang correspond à ce total de RP (recherche binaire + formule).</summary>
        public static int RankForRp(long rp)
        {
            if (rp <= 0) return 1;

            // Recherche binaire dans la table pour les 100 premiers rangs
            if (rp <= RpByRank[99])
            {
                int lo = 0, hi = RpByRank.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (RpByRank[mid] <= rp) lo = mid;
                    else hi = mid - 1;
                }
                return lo + 1;
            }

            // Formule inverse pour rang > 100 : résolution de 25x² + 23575x − (1023150 + rp) = 0
            // discriminant = 23575² + 100 * (1023150 + rp) = 658 096 725 + 100*rp
            double discriminant = 658_096_725.0 + 100.0 * rp;
            double x = (-23575.0 + Math.Sqrt(discriminant)) / 50.0;
            int rank = (int)Math.Floor(x);
            if (rank < 1) return 1;
            if (rank > MaxRank) return MaxRank;
            return rank;
        }

        /// <summary>Fixe le rang du personnage choisi (en écrivant le XP correspondant).</summary>
        public static OptionResult ApplyRank(OptionContext context, int rank)
        {
            rank = Math.Max(MinRank, Math.Min(MaxRank, rank));
            long rp = RpForRank(rank);
            return Write(context, rank, rp);
        }

        /// <summary>Fixe le RP du personnage choisi ; le rang affiché découle du XP.</summary>
        public static OptionResult ApplyRp(OptionContext context, long rp)
        {
            rp = Math.Max(0, Math.Min(MaxRp, rp));
            int rank = RankForRp(rp);
            return Write(context, rank, rp);
        }

        private static OptionResult Write(OptionContext context, int rank, long rp)
        {
            OptionResult result = new OptionResult();
            int rpInt = (int)Math.Min(rp, int.MaxValue);

            // Dans 1.57 PS4 EU, seule MP{n}_CHAR_XP_FM existe réellement. Le jeu recalcule
            // automatiquement le rang depuis le XP, donc une seule écriture suffit.
            context.WriteInt(result, context.CharacterStat("CHAR_XP_FM"), rpInt);

            context.SaveIfRequested(result);
            context.Log($"Rang {rank} appliqué · {rp:N0} RP (MP{context.Slot}).");
            return result;
        }
    }
}
