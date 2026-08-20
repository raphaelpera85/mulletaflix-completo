using System.Linq;
using Emby.Naming.Common;
using Emby.Naming.Video;
using MediaBrowser.Model.IO;
using Xunit;

namespace MulletaFlix.Naming.Tests.Video
{
    public partial class StackTests
    {
        private readonly NamingOptions _namingOptions = new NamingOptions();

        [Fact]
        public void TestSimpleStack()
        {
            var files = new[]
            {
                "Bad Boys (2006) part1.mkv",
                "Bad Boys (2006) part2.mkv",
                "Bad Boys (2006) part3.mkv",
                "Bad Boys (2006) part4.mkv",
                "Bad Boys (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);
            TestStackInfo(result[0], "Bad Boys (2006)", 4);
        }

        [Fact]
        public void TestFalsePositives()
        {
            var files = new[]
            {
                "Bad Boys (2006).mkv",
                "Bad Boys (2007).mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestFalsePositives2()
        {
            var files = new[]
            {
                "Bad Boys 2006.mkv",
                "Bad Boys 2007.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestFalsePositives3()
        {
            var files = new[]
            {
                "300 (2006).mkv",
                "300 (2007).mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestFalsePositives4()
        {
            var files = new[]
            {
                "300 2006.mkv",
                "300 2007.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestFalsePositives5()
        {
            var files = new[]
            {
                "Star Trek 1 - The motion picture.mkv",
                "Star Trek 2- The wrath of khan.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();
            Assert.Empty(result);
        }

        [Fact]
        public void TestFalsePositives6()
        {
            var files = new[]
            {
                "Red Riding in the Year of Our Lord 1983 (2009).mkv",
                "Red Riding in the Year of Our Lord 1980 (2009).mkv",
                "Red Riding in the Year of Our Lord 1974 (2009).mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestStackName()
        {
            var files = new[]
            {
                "d:/movies/300 2006 part1.mkv",
                "d:/movies/300 2006 part2.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);
            TestStackInfo(result[0], "300 2006", 2);
        }

        [Fact]
        public void ResolveFiles_GivenPartInMiddleOfName_ReturnsNoStack()
        {
            var files = new[]
            {
                "Bad Boys (2006).part1.stv.unrated.multi.1080p.bluray.x264-rough.mkv",
                "Bad Boys (2006).part2.stv.unrated.multi.1080p.bluray.x264-rough.mkv",
                "Bad Boys (2006).part3.stv.unrated.multi.1080p.bluray.x264-rough.mkv",
                "Bad Boys (2006).part4.stv.unrated.multi.1080p.bluray.x264-rough.mkv",
                "Bad Boys (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void ResolveFiles_FileNamesWithMissingPartType_ReturnsNoStack()
        {
            var files = new[]
            {
                "Bad Boys (2006).mkv",
                "Bad Boys (2006) 1.mkv",
                "Bad Boys (2006) 2.mkv",
                "Bad Boys (2006) 3.mkv",
                "Bad Boys (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void TestSimpleStackWithNumericName()
        {
            var files = new[]
            {
                "300 (2006) part1.mkv",
                "300 (2006) part2.mkv",
                "300 (2006) part3.mkv",
                "300 (2006) part4.mkv",
                "300 (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);
            TestStackInfo(result[0], "300 (2006)", 4);
        }

        [Fact]
        public void TestMixedExpressionsNotAllowed()
        {
            var files = new[]
            {
                "Bad Boys (2006) part1.mkv",
                "Bad Boys (2006) part2.mkv",
                "Bad Boys (2006) part3.mkv",
                "Bad Boys (2006) parta.mkv",
                "Bad Boys (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Single(result);
            TestStackInfo(result[0], "Bad Boys (2006)", 3);
        }

        [Fact]
        public void TestDualStacks()
        {
            var files = new[]
            {
                "Bad Boys (2006) part1.mkv",
                "Bad Boys (2006) part2.mkv",
                "Bad Boys (2006) part3.mkv",
                "Bad Boys (2006) part4.mkv",
                "Bad Boys (2006)-trailer.mkv",
                "300 (2006) part1.mkv",
                "300 (2006) part2.mkv",
                "300 (2006) part3.mkv",
                "300 (2006)-trailer.mkv"
            };

            var result = StackResolver.ResolveFiles(files, _namingOptions).ToList();

            Assert.Equal(2, result.Count);
            TestStackInfo(result[1], "Bad Boys (2006)", 4);
            TestStackInfo(result[0], "300 (2006)", 3);
        }
    }
}
