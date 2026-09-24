namespace PokemonCOSeedDataBaseAPI
{
    static class LCGExt
    {
        internal static uint Advance(ref this uint seed) { return seed = seed * 0x343FD + 0x269EC3; }
        internal static uint GetRand(ref this uint seed) { return seed.Advance() >> 16; }
        internal static uint GetRand(ref this uint seed, uint m) { return seed.GetRand() % m; }

        internal static uint Advance5(ref this uint seed) { return seed = seed * 0x284a930d + 0xa2974c77; }
    }
}
