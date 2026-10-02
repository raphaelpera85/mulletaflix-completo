using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.ScheduledTasks;

public sealed class ExpireLicensesTaskTests
{
    [Fact]
    public async Task ExecuteAsync_DisablesUsers_RecordsAggregateSuccessMetrics()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var licenseManager = new Mock<IUserLicenseManager>();
        licenseManager.Setup(manager => manager.ExpireOutdatedLicensesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var reportedProgress = new List<double>();
        var progress = new Mock<IProgress<double>>();
        progress.Setup(reporter => reporter.Report(It.IsAny<double>()))
            .Callback<double>(reportedProgress.Add);
        var task = CreateTask(licenseManager.Object);

        await task.ExecuteAsync(progress.Object, TestContext.Current.CancellationToken);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.license_expiration.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "success"));
        Assert.Single(measurements.Where(item => item.Name == "mulletaflix.license_expiration.duration"));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.license_expiration.users.disabled_per_run" && Equals(item.Value, 2L));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.license_expiration.active_runs")
            .Select(item => item.Value)
            .ToArray());
        Assert.DoesNotContain(measurements, item => item.Tags.Any(tag =>
            tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("id", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("name", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(new[] { 0d, 100d }, reportedProgress);
        licenseManager.Verify(manager => manager.ExpireOutdatedLicensesAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoExpiredLicenses_RecordsNoItems()
    {
        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var licenseManager = new Mock<IUserLicenseManager>();
        licenseManager.Setup(manager => manager.ExpireOutdatedLicensesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var task = CreateTask(licenseManager.Object);

        await task.ExecuteAsync(Mock.Of<IProgress<double>>(), TestContext.Current.CancellationToken);

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.license_expiration.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, "no_items"));
        Assert.Contains(measurements, item => item.Name == "mulletaflix.license_expiration.users.disabled_per_run" && Equals(item.Value, 0L));
    }

    [Theory]
    [InlineData(false, "failure")]
    [InlineData(true, "cancelled")]
    public async Task ExecuteAsync_Exception_RecordsFailureAndReleasesGauge(bool cancelled, string expectedResult)
    {
        using var cancellation = new CancellationTokenSource();
        if (cancelled)
        {
            cancellation.Cancel();
        }

        var measurements = new List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = Listen(measurements);
        var licenseManager = new Mock<IUserLicenseManager>();
        if (cancelled)
        {
            licenseManager.Setup(manager => manager.ExpireOutdatedLicensesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromCanceled<int>(cancellation.Token));
        }
        else
        {
            licenseManager.Setup(manager => manager.ExpireOutdatedLicensesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("test failure"));
        }

        var task = CreateTask(licenseManager.Object);

        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));
        }

        var run = Assert.Single(measurements.Where(item => item.Name == "mulletaflix.license_expiration.runs"));
        Assert.Contains(run.Tags, tag => tag.Key == "result" && Equals(tag.Value, expectedResult));
        Assert.Equal(new object[] { 1L, -1L }, measurements
            .Where(item => item.Name == "mulletaflix.license_expiration.active_runs")
            .Select(item => item.Value)
            .ToArray());
    }

    private static ExpireLicensesTask CreateTask(IUserLicenseManager licenseManager)
        => new(licenseManager, Mock.Of<ILocalizationManager>(), NullLogger<ExpireLicensesTask>.Instance);

    private static MeterListener Listen(List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == LicenseExpirationMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        return listener;
    }
}
