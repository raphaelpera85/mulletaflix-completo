using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.CatalogAudit;
using MediaBrowser.Model.CatalogAudit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Results;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class CatalogAuditControllerTests
{
    private readonly Mock<ICatalogAuditService> _auditServiceMock = new();

    private CatalogAuditController CreateController()
    {
        var controller = new CatalogAuditController(_auditServiceMock.Object);
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestAdmin"),
            new Claim(ClaimTypes.Role, "Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public void GetAudit_ReturnsReport()
    {
        var expectedReport = new CatalogAuditReport
        {
            TotalItemsScanned = 42,
            Inconsistencies =
            [
                new CatalogInconsistency
                {
                    ItemId = Guid.NewGuid(),
                    ItemName = "Sample",
                    Category = CatalogInconsistencyCategory.MissingProviderId
                }
            ]
        };

        _auditServiceMock.Setup(s => s.RunAudit(It.IsAny<CatalogAuditFilter>())).Returns(expectedReport);

        var controller = CreateController();
        var result = controller.GetAudit(null, null, null, null);

        var ok = Assert.IsType<OkResult<CatalogAuditReport>>(result.Result);
        var report = Assert.IsType<CatalogAuditReport>(ok.Value);
        Assert.Equal(42, report.TotalItemsScanned);
        Assert.Single(report.Inconsistencies);
    }

    [Fact]
    public async Task ApplyFix_ValidRequest_ReturnsHistoryEntry()
    {
        var historyEntry = new CatalogAuditHistoryEntry
        {
            ItemId = Guid.NewGuid(),
            Field = "ProductionYear",
            PreviousValue = "2000",
            AppliedValue = "1999"
        };

        _auditServiceMock.Setup(s => s.ApplyFixAsync(It.IsAny<CatalogAuditFixRequest>(), "TestAdmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(historyEntry);

        var controller = CreateController();
        var request = new CatalogAuditFixRequest
        {
            ItemId = historyEntry.ItemId,
            Field = "ProductionYear",
            ApprovedValue = "1999"
        };
        var result = await controller.ApplyFix(request, CancellationToken.None);

        var ok = Assert.IsType<OkResult<CatalogAuditHistoryEntry>>(result.Result);
        var entry = Assert.IsType<CatalogAuditHistoryEntry>(ok.Value);
        Assert.Equal("1999", entry.AppliedValue);
    }

    [Fact]
    public async Task RollbackFix_Success_ReturnsOk()
    {
        var historyId = Guid.NewGuid();
        _auditServiceMock.Setup(s => s.RollbackFixAsync(It.IsAny<CatalogAuditRollbackRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController();
        var request = new CatalogAuditRollbackRequest { HistoryEntryId = historyId };
        var result = await controller.RollbackFix(request, CancellationToken.None);

        var ok = Assert.IsType<OkResult<CatalogAuditRollbackResponse>>(result.Result);
        var response = Assert.IsType<CatalogAuditRollbackResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(historyId, response.HistoryEntryId);
    }

    [Fact]
    public void GetProviderTerms_ReturnsTermsList()
    {
        _auditServiceMock.Setup(s => s.GetProviderTerms()).Returns(
        [
            new ProviderTermsInfo { ProviderName = "OpenLibrary" }
        ]);

        var controller = CreateController();
        var result = controller.GetProviderTerms();

        var ok = Assert.IsType<OkResult<IReadOnlyList<ProviderTermsInfo>>>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ProviderTermsInfo>>(ok.Value);
        Assert.Single(list);
    }
}
