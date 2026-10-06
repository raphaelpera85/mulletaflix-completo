using System.Text;
using MulletaFlix.Api.Validation;
using Xunit;

namespace MulletaFlix.Api.Tests.Validation;

public sealed class LyricUploadValidatorTests
{
    [Theory]
    [InlineData("lrc", true)]
    [InlineData("LRC", true)]
    [InlineData("elrc", true)]
    [InlineData("txt", true)]
    [InlineData("srt", false)]
    [InlineData("exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedFormat_OnlyAcceptsFormatsTheParsersUnderstand(string? format, bool expected)
    {
        Assert.Equal(expected, LyricUploadValidator.IsAllowedFormat(format));
    }

    [Fact]
    public void LooksLikeBinaryContent_AcceptsPlainLyricText()
    {
        var content = Encoding.UTF8.GetBytes("[00:01.00]Hello world\n[00:02.00]Second line\n");
        Assert.False(LyricUploadValidator.LooksLikeBinaryContent(content));
    }

    [Theory]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 })] // MZ / PE
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 })] // ZIP
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })] // PNG
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })] // %PDF
    public void LooksLikeBinaryContent_RejectsKnownBinarySignatures(byte[] content)
    {
        Assert.True(LyricUploadValidator.LooksLikeBinaryContent(content));
    }

    [Fact]
    public void LooksLikeBinaryContent_RejectsEmbeddedNulByte()
    {
        var content = Encoding.UTF8.GetBytes("some text\0hidden-binary-tail");
        Assert.True(LyricUploadValidator.LooksLikeBinaryContent(content));
    }

    [Theory]
    [InlineData("[00:01.00]<script>alert(1)</script>")]
    [InlineData("hello javascript:alert(1)")]
    [InlineData("<?php system($_GET['c']); ?>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<iframe src=//evil.example></iframe>")]
    public void ContainsMaliciousPattern_DetectsKnownAttackPayloads(string text)
    {
        Assert.True(LyricUploadValidator.ContainsMaliciousPattern(text));
    }

    [Fact]
    public void ContainsMaliciousPattern_AllowsOrdinaryLyrics()
    {
        Assert.False(LyricUploadValidator.ContainsMaliciousPattern("[00:01.00]Never gonna give you up\n[00:02.00]Never gonna let you down"));
    }

    [Fact]
    public void Validate_AcceptsWellFormedLrcFile()
    {
        var content = Encoding.UTF8.GetBytes("[00:01.00]Hello world\n[00:02.00]Second line\n");
        Assert.Null(LyricUploadValidator.Validate("lrc", content));
    }

    [Fact]
    public void Validate_RejectsUnsupportedExtension()
    {
        var content = Encoding.UTF8.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nHello\n");
        Assert.NotNull(LyricUploadValidator.Validate("srt", content));
    }

    [Fact]
    public void Validate_RejectsBinaryPayloadMasqueradingAsLrc()
    {
        byte[] content = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        Assert.NotNull(LyricUploadValidator.Validate("lrc", content));
    }

    [Fact]
    public void Validate_RejectsEmbeddedScriptPayload()
    {
        var content = Encoding.UTF8.GetBytes("[00:01.00]<script>document.location='//evil.example'</script>");
        Assert.NotNull(LyricUploadValidator.Validate("txt", content));
    }
}
