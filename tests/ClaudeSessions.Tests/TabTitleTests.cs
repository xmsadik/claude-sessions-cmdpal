using ClaudeSessions.Core;
using Xunit;

namespace ClaudeSessions.Tests;

public class TabTitleTests
{
    [Theory]
    [InlineData("◑ Fix the build", "Fix the build")]
    [InlineData("✳ Fix the build", "Fix the build")]
    [InlineData("⠂  Claude code oturumlarının durumu ", "Claude code oturumlarının durumu")]
    [InlineData("Plain title", "Plain title")]
    [InlineData("2 files changed", "2 files changed")]
    [InlineData("◐◓ ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_StripsSpinnerPrefix(string? input, string expected) =>
        Assert.Equal(expected, TabTitle.Normalize(input));

    [Fact]
    public void Normalize_SpinnerFramesCompareEqual() =>
        Assert.Equal(TabTitle.Normalize("◑ Same topic"), TabTitle.Normalize("◒ Same topic"));
}
