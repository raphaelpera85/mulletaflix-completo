// SPDX-FileCopyrightText: 2026 MulletaFlix contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntroSkipper.Controllers;

/// <summary>
/// Serves only the non-sensitive static assets used to render the plugin configuration page.
/// The dashboard loads module scripts and stylesheets with native browser requests, which do
/// not carry the Jellyfin API token used by its authenticated page loader.
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
