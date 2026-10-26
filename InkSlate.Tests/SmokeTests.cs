using CsCheck;
using InkSlate.Core;
using Xunit;

namespace InkSlate.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void CoreFormatExtensionIsNotex()
    {
        Assert.Equal(".notex", InkSlateCore.FormatExtension);
    }

    [Fact]
    public void CsCheckPropertyRunnerWorks()
    {
        Gen.Int[1, 1000].Sample(n => n > 0);
    }
}

