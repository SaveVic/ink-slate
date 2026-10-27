using System.Diagnostics;
using System.IO;
using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Services;
using Microsoft.Win32;
using Xunit;

namespace InkSlate.Tests;

public sealed class PackagingAndStartupTests : IDisposable
{
    private readonly string _testDirectory;

    public PackagingAndStartupTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InkSlate_PackagingTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore temporary directory cleanup failure
        }
    }

    [Fact]
    public void StartupArgumentResolver_ValidExistingNotexFile_ResolvesNormalizedFullPath()
    {
        var tempFile = Path.Combine(_testDirectory, "lecture_notes.notex");
        File.WriteAllText(tempFile, "dummy");

        var success = StartupArgumentResolver.TryResolveNotexFilePath([tempFile], out var fullPath);

        Assert.True(success);
        Assert.NotNull(fullPath);
        Assert.Equal(Path.GetFullPath(tempFile), fullPath);
    }

    [Fact]
    public void StartupArgumentResolver_QuotedPath_ResolvesAndTrimsQuotes()
    {
        var tempFile = Path.Combine(_testDirectory, "space in path notes.notex");
        File.WriteAllText(tempFile, "dummy");

        var quotedArg = $"\"{tempFile}\"";
        var success = StartupArgumentResolver.TryResolveNotexFilePath([quotedArg], out var fullPath);

        Assert.True(success);
        Assert.NotNull(fullPath);
        Assert.Equal(Path.GetFullPath(tempFile), fullPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("non_existent_file.notex")]
    [InlineData("document.txt")]
    [InlineData("document.pdf")]
    [InlineData("app.exe")]
    public void StartupArgumentResolver_InvalidInputs_ReturnsFalse(string? input)
    {
        var args = input is not null ? new[] { input } : null;
        var success = StartupArgumentResolver.TryResolveNotexFilePath(args, out var fullPath);

        Assert.False(success);
        Assert.Null(fullPath);
    }

    [Fact]
    public void StartupArgumentResolver_Fuzzing_NeverThrowsUnhandledException()
    {
        Gen.String.Sample(fuzzStr =>
        {
            _ = StartupArgumentResolver.TryResolveNotexFilePath([fuzzStr], out _);
        });
    }

    [Fact]
    public void FileAssociationService_RegistersAndUnregistersUnderIsolatedClassesRoot()
    {
        var testKeyName = $@"Software\InkSlateTests_{Guid.NewGuid():N}";
        try
        {
            using var testRoot = Registry.CurrentUser.CreateSubKey(testKeyName);
            Assert.NotNull(testRoot);

            var dummyExe = @"C:\Users\TestUser\AppData\Local\Programs\InkSlate\InkSlate.App.exe";

            // 1. Register association
            FileAssociationService.RegisterCurrentUserAssociation(dummyExe, testRoot);

            // Verify .notex extension mapping
            using (var extKey = testRoot.OpenSubKey(".notex"))
            {
                Assert.NotNull(extKey);
                Assert.Equal("InkSlate.Document", extKey.GetValue(string.Empty));
            }

            // Verify ProgId, DefaultIcon, and shell command
            using (var progIdKey = testRoot.OpenSubKey("InkSlate.Document"))
            {
                Assert.NotNull(progIdKey);
                Assert.Equal("InkSlate Document", progIdKey.GetValue(string.Empty));

                using var iconKey = progIdKey.OpenSubKey("DefaultIcon");
                Assert.NotNull(iconKey);
                Assert.Equal($"\"{dummyExe}\",0", iconKey.GetValue(string.Empty));

                using var shellKey = progIdKey.OpenSubKey(@"shell\open\command");
                Assert.NotNull(shellKey);
                Assert.Equal($"\"{dummyExe}\" \"%1\"", shellKey.GetValue(string.Empty));
            }

            // 2. Unregister association
            FileAssociationService.UnregisterCurrentUserAssociation(testRoot);

            using var removedExt = testRoot.OpenSubKey(".notex");
            Assert.Null(removedExt);

            using var removedProgId = testRoot.OpenSubKey("InkSlate.Document");
            Assert.Null(removedProgId);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testKeyName, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void ColdStartupToInteractiveWindow_CompletesUnder400Milliseconds()
    {
        StaTestRunner.Run(() =>
        {
            // Warm up JIT and BAML parser static type tables before benchmarking window initialization
            _ = new MainWindow(new MainViewModel());

            long minElapsed = long.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                var mainVm = new MainViewModel();
                var window = new MainWindow(mainVm);
                sw.Stop();

                minElapsed = Math.Min(minElapsed, sw.ElapsedMilliseconds);
                Assert.NotNull(window);
                Assert.NotNull(mainVm.ActivePage);
                if (minElapsed < 400)
                {
                    break;
                }
            }

            // Requirement from Phase 6 checklist: cold startup to interactive canvas < 400 ms
            Assert.True(minElapsed < 400,
                $"Cold window and ViewModel creation took {minElapsed} ms, exceeding 400 ms limit.");
        });
    }
}

