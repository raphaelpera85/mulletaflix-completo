using System;
using System.Collections.Generic;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaStageSelectionTests
{
    [Fact]
    public void SelectBestStageDirectory_WhenFirstDiskBelow10Percent_SwitchesToSecondDisk()
    {
        var stageRoots = new[] { @"F:\NebulaStage", @"E:\NebulaStage" };
        var infoLogged = false;

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(
            stageRoots,
            logInfo: msg => infoLogged = true,
            driveInspector: root =>
            {
                if (root.StartsWith(@"F:", StringComparison.OrdinalIgnoreCase))
                {
                    // 5.1 GB livres de 447 GB (~1.14% livre, caso real do usuário)
                    return (true, 5L * 1024 * 1024 * 1024, 447L * 1024 * 1024 * 1024);
                }

                if (root.StartsWith(@"E:", StringComparison.OrdinalIgnoreCase))
                {
                    // 900 GB livres de 931 GB (~96.6% livre)
                    return (true, 900L * 1024 * 1024 * 1024, 931L * 1024 * 1024 * 1024);
                }

                return (false, 0, 0);
            });

        Assert.Equal(@"E:\NebulaStage", selected);
        Assert.True(infoLogged, "Deve logar mensagem informando a troca automática de disco devido à regra dos 10%.");
    }

    [Fact]
    public void SelectBestStageDirectory_WhenFirstDiskHasOver10Percent_KeepsFirstDisk()
    {
        var stageRoots = new[] { @"F:\NebulaStage", @"E:\NebulaStage" };

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(
            stageRoots,
            driveInspector: root =>
            {
                if (root.StartsWith(@"F:", StringComparison.OrdinalIgnoreCase))
                {
                    // 100 GB livres de 500 GB (20% livre)
                    return (true, 100L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024);
                }

                if (root.StartsWith(@"E:", StringComparison.OrdinalIgnoreCase))
                {
                    // 900 GB livres de 1000 GB (90% livre)
                    return (true, 900L * 1024 * 1024 * 1024, 1000L * 1024 * 1024 * 1024);
                }

                return (false, 0, 0);
            });

        Assert.Equal(@"F:\NebulaStage", selected);
    }

    [Fact]
    public void SelectBestStageDirectory_WhenAllDisksBelow10Percent_PicksDiskWithMostFreeSpace()
    {
        var stageRoots = new[] { @"F:\NebulaStage", @"E:\NebulaStage" };
        var warnLogged = false;

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(
            stageRoots,
            logWarning: msg => warnLogged = true,
            driveInspector: root =>
            {
                if (root.StartsWith(@"F:", StringComparison.OrdinalIgnoreCase))
                {
                    // 1 GB livre de 500 GB (0.2% livre)
                    return (true, 1L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024);
                }

                if (root.StartsWith(@"E:", StringComparison.OrdinalIgnoreCase))
                {
                    // 8 GB livres de 500 GB (1.6% livre, ambos < 10%)
                    return (true, 8L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024);
                }

                return (false, 0, 0);
            });

        Assert.Equal(@"E:\NebulaStage", selected);
        Assert.True(warnLogged, "Deve logar aviso quando todos os discos estiverem abaixo de 10%.");
    }

    [Fact]
    public void SelectBestStageDirectory_WhenRequiredBytesExceedsFreeSpace_SwitchesToDiskWithEnoughBytes()
    {
        var stageRoots = new[] { @"F:\NebulaStage", @"E:\NebulaStage" };

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(
            stageRoots,
            requiredBytes: 20L * 1024 * 1024 * 1024, // Requer 20 GB
            driveInspector: root =>
            {
                if (root.StartsWith(@"F:", StringComparison.OrdinalIgnoreCase))
                {
                    // 15 GB livres de 100 GB (15% livre, mas < 20 GB requeridos)
                    return (true, 15L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024);
                }

                if (root.StartsWith(@"E:", StringComparison.OrdinalIgnoreCase))
                {
                    // 50 GB livres de 100 GB (50% livre e >= 20 GB)
                    return (true, 50L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024);
                }

                return (false, 0, 0);
            });

        Assert.Equal(@"E:\NebulaStage", selected);
    }
}
