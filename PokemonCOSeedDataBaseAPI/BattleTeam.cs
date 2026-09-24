using System;

namespace PokemonCOSeedDataBaseAPI
{
    static class BattleTeamUltimate
    {
        public static (GenderRatio GenderRatio, Gender fixedGender, Nature fixedNature)[][] UltimateTeams = new (GenderRatio, Gender, Nature)[][]
        {
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.M7F1, Gender.Male, Nature.Sassy),
                (GenderRatio.M1F1, Gender.Female, Nature.Gentle),
                (GenderRatio.M1F1, Gender.Female, Nature.Modest),
                (GenderRatio.M1F1, Gender.Male, Nature.Rash),
                (GenderRatio.M1F3, Gender.Male, Nature.Naughty),
                (GenderRatio.M1F1, Gender.Female, Nature.Naughty),
            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.Genderless, Gender.Genderless, Nature.Hasty),
                (GenderRatio.M1F1, Gender.Female, Nature.Impish),
                (GenderRatio.M1F1, Gender.Male, Nature.Lonely),
                (GenderRatio.M1F1, Gender.Male, Nature.Mild),
                (GenderRatio.M1F1, Gender.Female, Nature.Mild),
                (GenderRatio.M1F1, Gender.Male, Nature.Serious),
            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.M7F1, Gender.Male, Nature.Brave),
                (GenderRatio.M3F1, Gender.Female, Nature.Mild),
                (GenderRatio.M1F1, Gender.Male, Nature.Modest),
                (GenderRatio.M1F1, Gender.Female, Nature.Bashful),
                (GenderRatio.M1F1, Gender.Male, Nature.Modest),
                (GenderRatio.M1F1, Gender.Female, Nature.Adamant),

            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.Genderless, Gender.Genderless, Nature.Mild),
                (GenderRatio.M1F3, Gender.Female, Nature.Rash),
                (GenderRatio.M1F1, Gender.Female, Nature.Adamant),
                (GenderRatio.M1F1, Gender.Female, Nature.Sassy),
                (GenderRatio.M7F1, Gender.Male, Nature.Adamant),
                (GenderRatio.M1F1, Gender.Male, Nature.Quirky),
            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.M7F1, Gender.Male, Nature.Quiet),
                (GenderRatio.M7F1, Gender.Male, Nature.Mild),
                (GenderRatio.M7F1, Gender.Male, Nature.Modest),
                (GenderRatio.M7F1, Gender.Male, Nature.Rash),
                (GenderRatio.M7F1, Gender.Male, Nature.Bold),
                (GenderRatio.M1F1, Gender.Female, Nature.Naughty),
            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.Genderless, Gender.Genderless, Nature.Modest),
                (GenderRatio.M1F1, Gender.Female, Nature.Quiet),
                (GenderRatio.Genderless, Gender.Genderless, Nature.Lonely),
                (GenderRatio.M1F1, Gender.Male, Nature.Adamant),
                (GenderRatio.Genderless, Gender.Genderless, Nature.Rash),
                (GenderRatio.M1F1, Gender.Female, Nature.Adamant),

            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.Genderless, Gender.Genderless, Nature.Lonely),
                (GenderRatio.M7F1, Gender.Male, Nature.Impish),
                (GenderRatio.M3F1, Gender.Male, Nature.Adamant),
                (GenderRatio.M1F1, Gender.Female, Nature.Lonely),
                (GenderRatio.M1F1, Gender.Female, Nature.Adamant),
                (GenderRatio.M3F1, Gender.Male, Nature.Adamant),
            },
            new (GenderRatio, Gender, Nature)[]
            {
                (GenderRatio.M1F1, Gender.Female, Nature.Adamant),
                (GenderRatio.M1F1, Gender.Male, Nature.Timid),
                (GenderRatio.M1F1, Gender.Female, Nature.Modest),
                (GenderRatio.M1F1, Gender.Male, Nature.Adamant),
                (GenderRatio.M1F1, Gender.Female, Nature.Modest),
                (GenderRatio.M1F1, Gender.Male, Nature.Adamant),
            },
        };

        public static bool GenerateTeamChecked(ref this uint seed, uint code)
        {
            var enemy = seed.GetRand() & 7;
            uint player;
            do { player = seed.GetRand() & 7; } while (player == enemy);
            if (player != code % 8) return false;

            var eTSV = seed.GetRand() ^ seed.GetRand();
            foreach (var pokemon in UltimateTeams[enemy])
                pokemon.Generate(ref seed, eTSV);

            if (seed.GetRand(3) != code / 8) return false;

            var pTSV = seed.GetRand() ^ seed.GetRand();
            foreach (var pokemon in UltimateTeams[player])
                pokemon.Generate(ref seed, pTSV);

            return true;
        }

        private static void Generate(this (GenderRatio GenderRatio, Gender fixedGender, Nature fixedNature) pokemon, ref uint seed, uint tsv)
        {
            seed.Advance5();
            while (true)
            {
                var hid = seed.GetRand();
                var lid = seed.GetRand();
                var pid = (hid << 16) | lid;
                var genderMatches =
                    pokemon.GenderRatio == GenderRatio.Genderless ||
                    ((lid & 0xFF) < (uint)pokemon.GenderRatio) == (pokemon.fixedGender == Gender.Female);

                if (genderMatches && pid % 25 == (uint)pokemon.fixedNature && (hid ^ lid ^ tsv) >= 8)
                {
                    return;
                }
            }
        }

        public static uint ToCode(this (PlayerName, BattleTeam) key)
            => (uint)key.Item1 * 8 + (uint)key.Item2;
        public static bool IsValid(this (PlayerName, BattleTeam) key)
            => Enum.IsDefined(typeof(PlayerName), key.Item1) && Enum.IsDefined(typeof(BattleTeam), key.Item2);
    }
}
