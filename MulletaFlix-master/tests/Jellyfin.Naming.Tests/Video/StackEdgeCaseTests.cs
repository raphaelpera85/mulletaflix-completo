using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MediaBrowser.Model.IO;
using Xunit;

namespace MulletaFlix.Naming.Tests.Video
{
    public partial class StackTests
    {
        [Fact]
        public void TestDirectories()
        {
            var files = new[]
            {
                "blah blah - cd 1",
                "blah blah - cd 2"
            };

            var result = StackResolver.ResolveDirectories(files, _namingOptions).ToList();

            Assert.Single(result);
            TestStackInfo(result[0], "blah blah", 2);
        }

        [Fact]
        public void TestMissingParttype()
        {
            var files = new[]
            {
                "300a.mkv",
                "300b.mkv",
                "300c.mkv",
                "300-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            // There should be no stack, because all files should be treated as separate movies
            Assert.Empty(result);
        }

        [Fact]
        public void TestFailSequence()
        {
            var files = new[]
            {
                "300 part1.mkv",
                "300 part2.mkv",
                "Avatar",
                "Avengers part1.mkv",
                "Avengers part2.mkv",
                "Avengers part3.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Equal(2, result.Count);

            TestStackInfo(result[0], "300", 2);
            TestStackInfo(result[1], "Avengers", 3);
        }

        [Fact]
        public void TestMixedExpressions()
        {
            var files = new[]
            {
                "Bad Boys (2006) part1.mkv",
                "Bad Boys (2006) part2.mkv",
                "Bad Boys (2006) part3.mkv",
                "Bad Boys (2006) part4.mkv",
                "Bad Boys (2006)-trailer.mkv",
                "300 (2006) parta.mkv",
                "300 (2006) partb.mkv",
                "300 (2006) partc.mkv",
                "300 (2006) partd.mkv",
                "300 (2006)-trailer.mkv",
                "300a.mkv",
                "300b.mkv",
                "300c.mkv",
                "300-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            // Only 'Bad Boys (2006)' and '300 (2006)' should be in the stack
            Assert.Equal(2, result.Count);

            TestStackInfo(result[0], "300 (2006)", 4);
            TestStackInfo(result[1], "Bad Boys (2006)", 4);
        }

        [Fact]
        public void TestAlphaLimitOfFour()
        {
            var files = new[]
            {
                "300 (2006) parta.mkv",
                "300 (2006) partb.mkv",
                "300 (2006) partc.mkv",
                "300 (2006) partd.mkv",
                "300 (2006) parte.mkv",
                "300 (2006) partf.mkv",
                "300 (2006) partg.mkv",
                "300 (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);

            TestStackInfo(result[0], "300 (2006)", 4);
        }

        [Fact]
        public void TestMixed()
        {
            var files = new[]
            {
                new FileSystemMetadata { FullName = "Bad Boys (2006) part1.mkv", IsDirectory = false },
                new FileSystemMetadata { FullName = "Bad Boys (2006) part2.mkv", IsDirectory = false },
                new FileSystemMetadata { FullName = "300 (2006) part2", IsDirectory = true },
                new FileSystemMetadata { FullName = "300 (2006) part3", IsDirectory = true },
                new FileSystemMetadata { FullName = "300 (2006) part1", IsDirectory = true }
            };

            var result = StackResolver.Resolve(files, _namingOptions).ToList();

            Assert.Equal(2, result.Count);
            TestStackInfo(result[0], "300 (2006)", 3);
            TestStackInfo(result[1], "Bad Boys (2006)", 2);
        }

        [Fact]
        public void TestNamesWithoutParts()
        {
            // No stacking here because there is no part/disc/etc
            var files = new[]
            {
                "Harry Potter and the Deathly Hallows.mkv",
                "Harry Potter and the Deathly Hallows 1.mkv",
                "Harry Potter and the Deathly Hallows 2.mkv",
                "Harry Potter and the Deathly Hallows 3.mkv",
                "Harry Potter and the Deathly Hallows 4.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestNumbersAppearingBeforePartNumber()
        {
            // No stacking here because there is no part/disc/etc
            var files = new[]
            {
                "Neverland (2011)[720p][PG][Voted 6.5][Family-Fantasy]part1.mkv",
                "Neverland (2011)[720p][PG][Voted 6.5][Family-Fantasy]part2.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        [Fact]
        public void TestMultiDiscs()
        {
            // No stacking here because there is no part/disc/etc
            var files = new[]
            {
                "M:/Movies (DVD)/Movies (Musical)/The Sound of Music/The Sound of Music (1965) (Disc 01)",
                "M:/Movies (DVD)/Movies (Musical)/The Sound of Music/The Sound of Music (1965) (Disc 02)"
            };

            var result = StackResolver.ResolveDirectories(files, _namingOptions).ToList();

            Assert.Single(result);
            Assert.Equal(2, result[0].Files.Count);
        }

        private void TestStackInfo(FileStack stack, string name, int fileCount)
        {
            Assert.Equal(fileCount, stack.Files.Count);
            Assert.Equal(name, stack.Name);
        }
    }
}

