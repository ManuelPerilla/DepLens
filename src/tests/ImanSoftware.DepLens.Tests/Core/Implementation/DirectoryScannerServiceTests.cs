using ImanSoftware.DepLens.Core.Implementation;

namespace ImanSoftware.DepLens.Tests.Core.Implementation;

public sealed class DirectoryScannerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DepLens-scanner-" + Guid.NewGuid());

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData("node_modules")]
    [InlineData("BIN")]
    [InlineData("Obj")]
    public async Task InvestigateDirectoryAsync_ExcludedDirectory_SkipsItsFiles(string excluded)
    {
        var expected = WriteFile("src/App/App.csproj");
        WriteFile($"src/App/{excluded}/Debug/Generated.csproj");
        WriteFile($"{excluded}/Directory.Build.props");

        var result = await new DirectoryScannerService().InvestigateDirectoryAsync(_root);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(expected, Assert.Single(result.Data).FullPath);
    }

    [Theory]
    [InlineData("binary")]
    [InlineData("objects")]
    [InlineData(".github")]
    [InlineData("node_modules_backup")]
    public async Task InvestigateDirectoryAsync_SimilarlyNamedDirectory_KeepsItsFiles(string directory)
    {
        var expected = WriteFile($"{directory}/App.csproj");

        var result = await new DirectoryScannerService().InvestigateDirectoryAsync(_root);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(expected, Assert.Single(result.Data).FullPath);
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("obj/repository")]
    public async Task InvestigateDirectoryAsync_ExcludedNameAtOrAboveScanRoot_KeepsSourceFiles(string directory)
    {
        var expected = WriteFile($"{directory}/App.csproj");
        var scanRoot = Path.GetDirectoryName(expected)!;

        var result = await new DirectoryScannerService().InvestigateDirectoryAsync(scanRoot);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(expected, Assert.Single(result.Data).FullPath);
    }

    private string WriteFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
