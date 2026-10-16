using System.Text.RegularExpressions;
using Xunit;

namespace NPTELManagement.Tests;

public class SignInButtonInteractionRegressionTests
{
    private readonly string _wwwrootPath;

    public SignInButtonInteractionRegressionTests()
    {
        // Locate Api/wwwroot relative to test assembly or repo root
        var currentDir = Directory.GetCurrentDirectory();
        var candidatePaths = new[]
        {
            Path.Combine(currentDir, "..", "Api", "wwwroot"),
            Path.Combine(currentDir, "..", "..", "..", "..", "Api", "wwwroot"),
            Path.Combine(currentDir, "Api", "wwwroot"),
            Path.GetFullPath("/home/child/Projects/NPTL-Management-pwa-phase1/Api/wwwroot")
        };

        _wwwrootPath = candidatePaths.FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException("Could not locate Api/wwwroot directory.");
    }

    [Fact]
    public void IndexHtml_LoginForm_HasNovalidateAndExplicitSignInButtonId()
    {
        var htmlContent = File.ReadAllText(Path.Combine(_wwwrootPath, "index.html"));

        // 1. Verify loginForm has novalidate attribute so HTML5 validation does not silently stop submission
        Assert.Matches(@"<form\s+id=""loginForm""[^>]*\bnovalidate\b[^>]*>", htmlContent);

        // 2. Verify Sign In button has explicit ID "signInBtn" and type="submit"
        Assert.Matches(@"<button\s+[^>]*id=""signInBtn""[^>]*type=""submit""[^>]*>\s*Sign in\s*</button>", htmlContent);

        // 3. Verify inputs have expected IDs
        Assert.Contains(@"id=""identifier""", htmlContent);
        Assert.Contains(@"id=""password""", htmlContent);
        Assert.Contains(@"id=""loginMessage""", htmlContent);
    }

    [Fact]
    public void AppJs_LoginHandler_PerformsClientValidationAndInFlightFeedback()
    {
        var jsContent = File.ReadAllText(Path.Combine(_wwwrootPath, "app.js"));

        // 1. References signInBtn element
        Assert.Contains(@"$(""signInBtn"")", jsContent);

        // 2. Client-side validation prevents silent failures and alerts user
        Assert.Contains("missing identifier", jsContent);
        Assert.Contains("missing password", jsContent);

        // 3. In-flight feedback sets button disabled and text to 'Signing in…'
        Assert.Contains(@"signInBtn.disabled = true", jsContent);
        Assert.Contains(@"signInBtn.textContent = ""Signing in…""", jsContent);

        // 4. In-flight feedback resets button in finally block
        Assert.Contains(@"signInBtn.disabled = false", jsContent);
    }

    [Fact]
    public void ServiceWorker_CacheVersion_MatchesAssetQueryVersion()
    {
        var htmlContent = File.ReadAllText(Path.Combine(_wwwrootPath, "index.html"));
        var swContent = File.ReadAllText(Path.Combine(_wwwrootPath, "sw.js"));

        // Extract cache name from sw.js
        var cacheMatch = Regex.Match(swContent, @"const\s+CACHE\s*=\s*""([^""]+)""");
        Assert.True(cacheMatch.Success, "CACHE constant must be defined in sw.js");
        var cacheName = cacheMatch.Groups[1].Value;

        // Verify cache version was bumped beyond stale v13
        Assert.NotEqual("nptel-pwa-shell-v13", cacheName);

        // Extract version query from app.js in index.html
        var appVersionMatch = Regex.Match(htmlContent, @"/app\.js\?v=([a-zA-Z0-9_\-]+)");
        Assert.True(appVersionMatch.Success, "app.js version query must be present in index.html");
        var versionQuery = appVersionMatch.Groups[1].Value;

        // Verify sw.js ASSETS array uses the exact same asset version query
        Assert.Contains($"/app.js?v={versionQuery}", swContent);
        Assert.Contains($"/styles.css?v={versionQuery}", swContent);
    }
}
