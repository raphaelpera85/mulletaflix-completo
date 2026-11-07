using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Moq;
using Xunit;

namespace MulletaFlix.Controller.Tests.Entities;

public sealed class CollectionFolderTests
{
    [Fact]
    public void SaveLibraryOptions_WhenSerializationFails_PreservesCachedOptions()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.DeserializeFromFile(typeof(LibraryOptions), Path.Combine(path, "options.xml")))
            .Throws<FileNotFoundException>();
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Throws(new IOException("Simulated XML serialization failure."));

        var originalSerializer = CollectionFolder.XmlSerializer;
        var updateEventRaised = false;
        EventHandler<LibraryOptionsUpdatedEventArgs> handler = (_, _) => updateEventRaised = true;
        CollectionFolder.LibraryOptionsUpdated += handler;
        CollectionFolder.XmlSerializer = serializer.Object;
        try
        {
            var cachedOptions = CollectionFolder.GetLibraryOptions(path);

            Assert.Throws<IOException>(() => CollectionFolder.SaveLibraryOptions(path, new LibraryOptions()));

            Assert.Same(cachedOptions, CollectionFolder.GetLibraryOptions(path));
            Assert.False(updateEventRaised);
        }
        finally
        {
            CollectionFolder.LibraryOptionsUpdated -= handler;
            CollectionFolder.XmlSerializer = originalSerializer;
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void SaveLibraryOptions_WhenSerializationFails_PreservesExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var optionsPath = Path.Combine(path, "options.xml");
        const string originalContent = "original options";
        File.WriteAllText(optionsPath, originalContent);

        var serializer = new Mock<IXmlSerializer>();
        var cachedOptions = new LibraryOptions();
        serializer
            .Setup(instance => instance.DeserializeFromFile(typeof(LibraryOptions), optionsPath))
            .Returns(cachedOptions);
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Callback<object, Stream>((_, stream) => stream.Write(System.Text.Encoding.UTF8.GetBytes("partial options")))
            .Throws(new IOException("Simulated XML serialization failure."));

        var originalSerializer = CollectionFolder.XmlSerializer;
        CollectionFolder.XmlSerializer = serializer.Object;
        try
        {
            Assert.Same(cachedOptions, CollectionFolder.GetLibraryOptions(path));

            Assert.Throws<IOException>(() => CollectionFolder.SaveLibraryOptions(path, new LibraryOptions()));

            Assert.Equal(originalContent, File.ReadAllText(optionsPath));
            Assert.Same(cachedOptions, CollectionFolder.GetLibraryOptions(path));
            Assert.Empty(Directory.GetFiles(path, "options.xml.*.tmp"));
        }
        finally
        {
            CollectionFolder.XmlSerializer = originalSerializer;
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void SaveLibraryOptions_ReplacesExistingFileAfterSerialization()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var optionsPath = Path.Combine(path, "options.xml");
        File.WriteAllText(optionsPath, "old options");

        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.DeserializeFromFile(typeof(LibraryOptions), optionsPath))
            .Returns(new LibraryOptions());
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Callback<object, Stream>((_, stream) => stream.Write(System.Text.Encoding.UTF8.GetBytes("new options")));

        var originalSerializer = CollectionFolder.XmlSerializer;
        CollectionFolder.XmlSerializer = serializer.Object;
        try
        {
            CollectionFolder.SaveLibraryOptions(path, new LibraryOptions());

            Assert.Equal("new options", File.ReadAllText(optionsPath));
            Assert.Empty(Directory.GetFiles(path, "options.xml.*.tmp"));
        }
        finally
        {
            CollectionFolder.XmlSerializer = originalSerializer;
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void SaveLibraryOptions_PreservesUnixModeWhenAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var optionsPath = Path.Combine(path, "options.xml");
        File.WriteAllText(optionsPath, "old options");
        const UnixFileMode expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.SetUnixFileMode(optionsPath, expectedMode);

        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Callback<object, Stream>((_, stream) => stream.Write(System.Text.Encoding.UTF8.GetBytes("new options")));

        var originalSerializer = CollectionFolder.XmlSerializer;
        CollectionFolder.XmlSerializer = serializer.Object;
        try
        {
            CollectionFolder.SaveLibraryOptions(path, new LibraryOptions());

            Assert.Equal(expectedMode, File.GetUnixFileMode(optionsPath));
        }
        finally
        {
            CollectionFolder.XmlSerializer = originalSerializer;
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task SaveLibraryOptions_SerializesConcurrentWrites()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var serializer = new Mock<IXmlSerializer>();
        serializer
            .Setup(instance => instance.DeserializeFromFile(typeof(LibraryOptions), Path.Combine(path, "options.xml")))
            .Throws<FileNotFoundException>();

        using var firstSerializationStarted = new ManualResetEventSlim();
        using var releaseFirstSerialization = new ManualResetEventSlim();
        var serializationCount = 0;
        var activeSerializations = 0;
        serializer
            .Setup(instance => instance.SerializeToStream(It.IsAny<object>(), It.IsAny<Stream>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref activeSerializations) != 1)
                {
                    Interlocked.Decrement(ref activeSerializations);
                    throw new InvalidOperationException("Concurrent XML serialization detected.");
                }

                try
                {
                    if (Interlocked.Increment(ref serializationCount) == 1)
                    {
                        firstSerializationStarted.Set();
                        if (!releaseFirstSerialization.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
                        {
                            throw new TimeoutException("Timed out waiting to release the first serialization.");
                        }
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeSerializations);
                }
            });

        var originalSerializer = CollectionFolder.XmlSerializer;
        CollectionFolder.XmlSerializer = serializer.Object;
        try
        {
            var first = Task.Run(() => CollectionFolder.SaveLibraryOptions(path, new LibraryOptions()), TestContext.Current.CancellationToken);
            Assert.True(firstSerializationStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            using var secondCallStarted = new ManualResetEventSlim();
            var second = Task.Run(
                () =>
                {
                    secondCallStarted.Set();
                    CollectionFolder.SaveLibraryOptions(path, new LibraryOptions());
                },
                TestContext.Current.CancellationToken);

            Assert.True(secondCallStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            var completedTask = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
            Assert.NotSame(second, completedTask);
            releaseFirstSerialization.Set();

            await Task.WhenAll(first, second);
            Assert.Equal(2, serializationCount);
            Assert.Equal(0, activeSerializations);
        }
        finally
        {
            releaseFirstSerialization.Set();
            CollectionFolder.XmlSerializer = originalSerializer;
            Directory.Delete(path, recursive: true);
        }
    }
}
