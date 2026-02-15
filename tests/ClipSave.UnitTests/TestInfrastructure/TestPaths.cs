using System.IO;

namespace ClipSave.UnitTests;

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    public static string SourceRoot { get; } = Path.Combine(RepositoryRoot, "src", "ClipSave");
}
