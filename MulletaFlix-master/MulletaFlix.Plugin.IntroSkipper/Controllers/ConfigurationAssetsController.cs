// SPDX-FileCopyrightText: 2026 MulletaFlix contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntroSkipper.Controllers;

/// <summary>
/// Serves only the static assets required to render Intro Skipper's configuration page.
/// Module and stylesheet requests do not include the dashboard API token.
/// </summary>
[ApiController]
[Route("IntroSkipper/Configuration")]
public sealed class ConfigurationAssetsController : ControllerBase
{
    private const string ResourcePrefix = "IntroSkipper.Configuration.";

    /// <summary>Returns the configuration page JavaScript.</summary>
    [HttpGet("introskipper.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public IActionResult GetJavaScript() => GetAsset("introskipper.js", "application/javascript");

    /// <summary>Returns the configuration page stylesheet.</summary>
    [HttpGet("introskipper.css")]
    [AllowAnonymous]
    [Produces("text/css")]
    public IActionResult GetStylesheet() => GetAsset("introskipper.css", "text/css");

    private IActionResult GetAsset(string name, string contentType)
    {
        var stream = typeof(Plugin).Assembly.GetManifestResourceStream(ResourcePrefix + name);
        return stream is null ? NotFound() : File(stream, contentType);
    }
}
