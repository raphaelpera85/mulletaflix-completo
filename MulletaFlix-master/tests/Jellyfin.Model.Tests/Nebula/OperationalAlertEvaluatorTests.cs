using System;
using System.Collections.Generic;
using MediaBrowser.Model.Nebula;
using Xunit;

namespace MulletaFlix.Model.Tests.Nebula
{
    public class OperationalAlertEvaluatorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        private static OperationalAlertInputs BaseInputs() => new()
        {
            NowUtc = Now,
            LastSuccessfulBackupUtc = Now.AddDays(-1)
        };

        [Fact]
        public void Evaluate_WithHealthySignals_ReturnsNoAlerts()
        {
            var inputs = BaseInputs();

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Empty(alerts);
        }

        [Fact]
        public void Evaluate_ThrowsOnNullInputs()
        {
            Assert.Throws<ArgumentNullException>(() => OperationalAlertEvaluator.Evaluate(null!));
        }

        [Theory]
        [InlineData(119, false)]
        [InlineData(120, true)]
        [InlineData(200, true)]
        public void Evaluate_QueueStalled_FiresAtOrAboveThreshold(int ageMinutes, bool expectAlert)
        {
            var inputs = BaseInputs();
            inputs.OldestPendingUploadAtUtc = Now.AddMinutes(-ageMinutes);
            inputs.QueueStalledThresholdMinutes = 120;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Equal(expectAlert, Contains(alerts, OperationalAlertKind.QueueStalled));
        }

        [Fact]
        public void Evaluate_QueueStalled_NoAlertWhenNoPendingUpload()
        {
            var inputs = BaseInputs();
            inputs.OldestPendingUploadAtUtc = null;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.False(Contains(alerts, OperationalAlertKind.QueueStalled));
        }

        [Fact]
        public void Evaluate_BackupOverdue_CriticalWhenNeverBackedUp()
        {
            var inputs = BaseInputs();
            inputs.LastSuccessfulBackupUtc = null;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            var alert = Assert.Single(alerts, a => a.Kind == OperationalAlertKind.BackupOverdue);
            Assert.Equal(OperationalAlertSeverity.Critical, alert.Severity);
        }

        [Theory]
        [InlineData(6, false)]
        [InlineData(7, true)]
        [InlineData(10, true)]
        public void Evaluate_BackupOverdue_WarningAtOrAboveThresholdDays(int ageDays, bool expectAlert)
        {
            var inputs = BaseInputs();
            inputs.LastSuccessfulBackupUtc = Now.AddDays(-ageDays);
            inputs.BackupOverdueThresholdDays = 7;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Equal(expectAlert, Contains(alerts, OperationalAlertKind.BackupOverdue));
            if (expectAlert)
            {
                var alert = Assert.Single(alerts, a => a.Kind == OperationalAlertKind.BackupOverdue);
                Assert.Equal(OperationalAlertSeverity.Warning, alert.Severity);
            }
        }

        [Theory]
        [InlineData(0.11, false)]
        [InlineData(0.10, true)]
        [InlineData(0.01, true)]
        public void Evaluate_DiskSpaceLow_FiresAtOrBelowCriticalRatio(double ratio, bool expectAlert)
        {
            var inputs = BaseInputs();
            inputs.StorageFreeRatioByRole = new Dictionary<string, double> { ["cache"] = ratio };
            inputs.DiskSpaceCriticalRatio = 0.10;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Equal(expectAlert, Contains(alerts, OperationalAlertKind.DiskSpaceLow));
        }

        [Fact]
        public void Evaluate_DiskSpaceLow_ReportsOneAlertPerAffectedRole()
        {
            var inputs = BaseInputs();
            inputs.StorageFreeRatioByRole = new Dictionary<string, double>
            {
                ["cache"] = 0.02,
                ["backup"] = 0.05,
                ["transcode"] = 0.50
            };
            inputs.DiskSpaceCriticalRatio = 0.10;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            var diskAlerts = new List<OperationalAlertDto>();
            foreach (var alert in alerts)
            {
                if (alert.Kind == OperationalAlertKind.DiskSpaceLow)
                {
                    diskAlerts.Add(alert);
                }
            }

            Assert.Equal(2, diskAlerts.Count);
        }

        [Theory]
        [InlineData(4, false)]
        [InlineData(5, true)]
        [InlineData(9, true)]
        public void Evaluate_RepeatedProviderError_FiresAtOrAboveThreshold(long count, bool expectAlert)
        {
            var inputs = BaseInputs();
            inputs.RecentFailuresByStage = new[]
            {
                new NebulaUploadFailureStageCountDto { Stage = NebulaUploadFailureStages.TelegramTransfer, Count = count }
            };
            inputs.RepeatedProviderErrorThreshold = 5;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Equal(expectAlert, Contains(alerts, OperationalAlertKind.RepeatedProviderError));
        }

        [Fact]
        public void Evaluate_RestoreFailed_CriticalWhenLastRestoreFailed()
        {
            var inputs = BaseInputs();
            inputs.LastRestoreFailed = true;
            inputs.LastRestoreFailureMessage = "HTTP 503";

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            var alert = Assert.Single(alerts, a => a.Kind == OperationalAlertKind.RestoreFailed);
            Assert.Equal(OperationalAlertSeverity.Critical, alert.Severity);
            Assert.Contains("HTTP 503", alert.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Evaluate_RestoreFailed_NoAlertWhenLastRestoreSucceeded()
        {
            var inputs = BaseInputs();
            inputs.LastRestoreFailed = false;

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.False(Contains(alerts, OperationalAlertKind.RestoreFailed));
        }

        [Fact]
        public void Evaluate_OrdersCriticalAlertsBeforeWarnings()
        {
            var inputs = BaseInputs();
            inputs.LastSuccessfulBackupUtc = Now.AddDays(-10); // warning
            inputs.LastRestoreFailed = true; // critical

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.Equal(OperationalAlertSeverity.Critical, alerts[0].Severity);
            Assert.Equal(OperationalAlertKind.RestoreFailed, alerts[0].Kind);
        }

        [Fact]
        public void Evaluate_MessagesDoNotLeakPathsOrIdentifiers()
        {
            var inputs = BaseInputs();
            inputs.OldestPendingUploadAtUtc = Now.AddMinutes(-500);
            inputs.LastSuccessfulBackupUtc = null;
            inputs.StorageFreeRatioByRole = new Dictionary<string, double> { ["cache"] = 0.01 };
            inputs.RecentFailuresByStage = new[]
            {
                new NebulaUploadFailureStageCountDto { Stage = NebulaUploadFailureStages.TelegramTransfer, Count = 10 }
            };
            inputs.LastRestoreFailed = true;
            inputs.LastRestoreFailureMessage = "timeout";

            var alerts = OperationalAlertEvaluator.Evaluate(inputs);

            Assert.NotEmpty(alerts);
            foreach (var alert in alerts)
            {
                Assert.DoesNotContain(@"\", alert.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("/", alert.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("mongodb://", alert.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool Contains(IReadOnlyList<OperationalAlertDto> alerts, string kind)
        {
            foreach (var alert in alerts)
            {
                if (alert.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
