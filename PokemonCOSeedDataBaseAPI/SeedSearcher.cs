using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PokemonCOSeedDataBaseAPI
{
    public sealed class SeedSearcher : IDisposable
    {
        private const int HEADER_SIZE = 32;
        private const uint MAGIC = 0x42_44_4F_43; // "CODB"
        private const int VERSION = 1;

        private const int PREFIX_COUNT = 24 * 24 * 24 * 24 * 24;

        private readonly FileStream _stream;

        public SeedSearcher(string path)
        {
            var opened = File.OpenRead(path);
            try
            {
                using var reader = new BinaryReader(opened, Encoding.UTF8, leaveOpen: true);

                if (opened.Length < HEADER_SIZE) throw new InvalidDataException("DB header is truncated.");
                if (reader.ReadUInt32() != MAGIC) throw new InvalidDataException("Invalid DB magic.");
                if (reader.ReadUInt32() != VERSION) throw new InvalidDataException("Unsupported DB version.");

                var seedSectionOffset = reader.ReadUInt64();
                var checksum = reader.ReadUInt64();
                var riceK = reader.ReadByte();

                if (seedSectionOffset < HEADER_SIZE) throw new InvalidDataException("Invalid DB layout.");
                if (seedSectionOffset > (ulong)opened.Length) throw new InvalidDataException("Invalid DB layout.");
                if ((seedSectionOffset - HEADER_SIZE) % 8 != 0) throw new InvalidDataException("Invalid DB layout.");
                if (((ulong)opened.Length - seedSectionOffset) % 2 != 0) throw new InvalidDataException("Invalid DB layout.");
                if (riceK > 12) throw new InvalidDataException("Invalid DB layout.");

                opened.Position = HEADER_SIZE;

                var countsLength = (int)(seedSectionOffset - HEADER_SIZE);
                var counts = reader.ReadBytes(countsLength);
                if (counts.Length != countsLength) throw new EndOfStreamException();

                const int CHECKPOINT_COUNT = (PREFIX_COUNT + CHECKPOINT_STRIDE - 1) / CHECKPOINT_STRIDE;
                var checkpoints = new (ulong, long)[CHECKPOINT_COUNT];
                ulong total = 0;
                {
                    var csReader = new CountsSectionReader(riceK, counts);
                    for (int i = 0; i < PREFIX_COUNT; i++)
                    {
                        if (i % CHECKPOINT_STRIDE == 0)
                        {
                            var checkpoint = i / CHECKPOINT_STRIDE;
                            checkpoints[checkpoint] = (total, csReader.BitPosition);
                        }
                        total += csReader.ReadCount();
                    }
                }

                // validate total
                {
                    var entryCount = ((ulong)opened.Length - seedSectionOffset) / 2;
                    if (total != entryCount)
                        throw new InvalidDataException("Count total does not match the seed section.");
                }

                // validate hash
                {
                    var hash = new Fnv1A(Fnv1A.OFFSET);
                    hash.Update(counts, counts.Length);
                    {
                        var buffer = new byte[65536];
                        while (true)
                        {
                            var read = opened.Read(buffer, 0, buffer.Length);
                            if (read == 0) break;

                            hash.Update(buffer, read);
                        }
                    }

                    if (hash.Value != checksum) throw new InvalidDataException("DB checksum mismatch.");
                }

                _stream = opened;
                _seedSectionOffset = seedSectionOffset;
                _riceK = riceK;
                _countsSection = counts;
                _checkpoints = checkpoints;
            }
            catch
            {
                opened.Dispose();
                throw;
            }
        }

        public SeedSearchResult Search((PlayerName, BattleTeam)[] keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Length != 5) throw new ArgumentException("Exactly five search keys are required.", nameof(keys));
            if (!keys.All((_) => _.IsValid())) throw new ArgumentOutOfRangeException(nameof(keys), "A search key is out of range.");

            var codes = keys.Select((_) => _.ToCode()).ToArray();
            var prefix = codes.Aggregate(0, (acc, cur) => acc * 24 + (int)cur);

            var (first, last) = ReadRange(prefix);
            if (first == last) return new SeedSearchResult(Enumerable.Empty<uint>());

            var seeds = new ushort[(int)(last - first)];
            {
                _stream.Position = (long)(_seedSectionOffset + first * 2);
                using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
                for (int i = 0; i < seeds.Length; i++)
                    seeds[i] = reader.ReadUInt16();
            }

            return new SeedSearchResult(SearchSeeds(codes, seeds).ToArray());
        }

        private readonly ulong _seedSectionOffset;
        private static IEnumerable<uint> SearchSeeds(uint[] codes, ushort[] seeds)
        {
            foreach (var s in seeds)
            {
                var h16 = (uint)s << 16;
                for (uint l16 = 0; l16 <= ushort.MaxValue; l16++)
                {
                    var seed = h16 | l16;

                    var matched = codes.All((code) => seed.GenerateTeamChecked(code));
                    if (matched) yield return seed;
                }
            }
        }

        private readonly byte _riceK;

        // 『各prefixに対応するseedの個数をRice符号化して並べたビット列』をそのままbyte配列として持つ。
        private readonly byte[] _countsSection;
        private readonly (ulong Total, long BitPosition)[] _checkpoints;
        private const int CHECKPOINT_STRIDE = 256;
        private (ulong first, ulong last) ReadRange(int prefix)
        {
            // NOTE: _countsSectionはそこそこサイズがデカいので詰めてbyte配列のまま持っているが、
            // 符号のビット長はprefixごとに異なるため、そのままではランダムアクセスできない。
            // そこで、CHECKPOINT_STRIDEごとにcheckpointを記録しておき、そこから線形走査することで、
            // 任意のprefixに対応するseedの個数を現実的な時間で読み出せるようにしている。

            var checkpointPos = prefix / CHECKPOINT_STRIDE;
            var (total, pos) = _checkpoints[checkpointPos];

            var csReader = new CountsSectionReader(_riceK, _countsSection, pos);

            var first = total;
            for (int i = checkpointPos * CHECKPOINT_STRIDE; i < prefix; i++)
                first += csReader.ReadCount();
            var last = first + csReader.ReadCount();

            return (first, last);
        }

        public void Dispose() => _stream.Dispose();
    }

    public sealed class SeedSearchResult : IEnumerable<uint>
    {
        private readonly IEnumerable<uint> _seeds;

        internal SeedSearchResult(IEnumerable<uint> seeds) => _seeds = seeds;

        public SeedSearchResult Search((PlayerName, BattleTeam) key)
        {
            if (!key.IsValid()) throw new ArgumentOutOfRangeException(nameof(key), "A search key is out of range.");

            var code = key.ToCode();
            return new SeedSearchResult(SearchSeeds(_seeds, code));
        }

        private static IEnumerable<uint> SearchSeeds(IEnumerable<uint> seeds, uint code)
        {
            foreach (var current in seeds)
            {
                var seed = current;
                if (seed.GenerateTeamChecked(code)) yield return seed;
            }
        }

        public IEnumerator<uint> GetEnumerator() => _seeds.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    sealed class CountsSectionReader
    {
        private readonly byte _k;

        private readonly byte[] bytes;
        private int byteIndex;
        private int bitIndex;

        public CountsSectionReader(byte k, byte[] bytes, long bitPosition = 0)
        {
            _k = k;
            this.bytes = bytes;
            byteIndex = (int)(bitPosition / 8);
            bitIndex = (int)(bitPosition % 8);
        }

        public long BitPosition => (long)byteIndex * 8 + bitIndex;

        public uint ReadCount()
        {
            var quotient = 0u;
            while (ReadBit() != 0)
            {
                if (quotient == (uint.MaxValue >> _k))
                    throw new InvalidDataException("Rice value is too large.");
                quotient++;
            }

            var remainder = 0u;
            for (int i = 0; i < _k; i++) remainder |= ReadBit() << i;
            var value = (quotient << _k) | remainder;
            var count = 16L + (value >> 1 ^ -(value & 1));
            if (count < 0 || count > uint.MaxValue)
                throw new InvalidDataException("Invalid count value.");

            return (uint)count;
        }

        private uint ReadBit()
        {
            if (byteIndex >= bytes.Length) throw new InvalidDataException("Counts section is truncated.");

            var bit = (uint)((bytes[byteIndex] >> bitIndex) & 1);
            if (++bitIndex == 8)
            {
                bitIndex = 0;
                byteIndex++;
            }

            return bit;
        }
    }

    struct Fnv1A
    {
        public const ulong OFFSET = 0xCBF29CE484222325;
        private const ulong PRIME = 0x100000001B3;

        public ulong Value { get; set; }

        public Fnv1A(ulong value) => Value = value;

        public void Update(byte[] bytes, int length)
        {
            for (int i = 0; i < length; i++)
                Value = (Value ^ bytes[i]) * PRIME;
        }

    }
}
