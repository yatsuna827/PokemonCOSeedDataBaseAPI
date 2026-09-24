using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PokemonCOSeedDataBaseAPI.Tests
{
    public sealed class SeedSearcherTests
    {
        // CODBファイルはでかいのでGit管理しない。テスト実行前にTestData/CODBを配置してください。
        private static string DatabasePath => Path.Combine(AppContext.BaseDirectory, "TestData", "CODB");

        [Fact]
        public void DatabaseFixtureContainsFullCycle()
        {
            Assert.Equal(260314946, new FileInfo(DatabasePath).Length);
        }

        [Fact]
        public void SearchFindsKnownSeedsAcrossMultiplePrefixes()
        {
            using var searcher = new SeedSearcher(DatabasePath);

            Assert.Equal(new uint[] { 0x3956FB2C },
                searcher.Search(Keys(11, 16, 13, 13, 23)).Search(Key(10)).Search(Key(10)));
            Assert.Equal(new uint[] { 0x3956FB2C },
                searcher.Search(Keys(20, 0, 15, 14, 21)).Search(Key(14)).Search(Key(18)));
        }

        [Theory]
        [InlineData(16, 15, 23, 3, 1, 10, 10, 0x3956FB2Cu)]
        [InlineData(3, 15, 15, 4, 5, 14, 18, 0x3956FB2Cu)]
        [InlineData(17, 22, 4, 5, 14, 18, 8, 0xD003DFFBu)]
        [InlineData(13, 9, 15, 21, 19, 4, 6, 0xD9825EF0u)]
        [InlineData(5, 18, 12, 13, 22, 2, 23, 0x948113DDu)]
        [InlineData(23, 5, 22, 7, 10, 12, 21, 0x01196E55u)]
        public void SearchFindsAdditionalKnownSeeds(
            uint c1, uint c2, uint c3, uint c4, uint c5, uint c6, uint c7, uint expected)
        {
            using var searcher = new SeedSearcher(DatabasePath);

            Assert.Equal(new uint[] { expected },
                searcher.Search(Keys(c1, c2, c3, c4, c5)).Search(Key(c6)).Search(Key(c7)));
        }

        [Fact]
        public void SearchReturnsEveryMatchingSeed()
        {
            using var searcher = new SeedSearcher(DatabasePath);

            Assert.Equal(
                new uint[] { 0x3A7A240A, 0x632F181B },
                searcher.Search(Keys(1, 1, 7, 3, 21)).Search(Key(22)).Search(Key(10)));
            Assert.Equal(
                new uint[] { 0x80EFE8C4, 0x9A5352BC },
                searcher.Search(Keys(20, 3, 18, 4, 15)).Search(Key(19)).Search(Key(14)));
        }

        [Fact]
        public void SearchReturnsNoResultsForEmptyPrefix()
        {
            using var searcher = new SeedSearcher(DatabasePath);

            Assert.Empty(searcher.Search(Keys(0, 0, 0, 0, 0)).Search(Key(0)).Search(Key(0)));
        }

        [Fact]
        public void SearchCanContinueAfterSearcherIsDisposed()
        {
            SeedSearchResult result;
            using (var searcher = new SeedSearcher(DatabasePath))
                result = searcher.Search(Keys(11, 16, 13, 13, 23));

            Assert.NotEmpty(result);
            Assert.Equal(new uint[] { 0x3956FB2C }, result.Search(Key(10)).Search(Key(10)));
        }

        [Fact]
        public void SearchRejectsInvalidKeys()
        {
            using var searcher = new SeedSearcher(DatabasePath);

            Assert.Throws<ArgumentException>(() => searcher.Search(Keys(0, 0, 0, 0, 0, 0)));
            var keys = Keys(0, 0, 0, 0, 0);
            keys[0] = ((PlayerName)3, BattleTeam.Blazikin);
            Assert.Throws<ArgumentOutOfRangeException>(() => searcher.Search(keys));
            var result = searcher.Search(Keys(11, 16, 13, 13, 23));
            Assert.Throws<ArgumentOutOfRangeException>(() => result.Search(((PlayerName)3, BattleTeam.Blazikin)));
        }

        [Fact]
        public void ConstructorRejectsCorruptedSeedSection()
        {
            var path = Path.Combine(Path.GetTempPath(), $"codb-test-{Guid.NewGuid():N}.codb");
            try
            {
                File.Copy(DatabasePath, path);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    stream.Position = stream.Length - 1;
                    var last = stream.ReadByte();
                    stream.Position--;
                    stream.WriteByte((byte)(last ^ 1));
                }

                Assert.Throws<InvalidDataException>(() => new SeedSearcher(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static (PlayerName, BattleTeam)[] Keys(params uint[] codes)
        {
            return codes.Select(Key).ToArray();
        }

        private static (PlayerName, BattleTeam) Key(uint code)
        {
            return ((PlayerName)(code / 8), (BattleTeam)(code % 8));
        }
    }
}
