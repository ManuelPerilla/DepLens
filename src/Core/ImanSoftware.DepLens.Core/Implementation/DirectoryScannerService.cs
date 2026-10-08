using ImanSoftware.DepLens.Abstractions.Models;
using ImanSoftware.DepLens.Abstractions.Services;
using ImanSoftware.Extensions;
using ImanSoftware.FileStorage;
using ImanSoftware.Outcomes;
using System.Collections.Immutable;

namespace ImanSoftware.DepLens.Core.Implementation;

internal sealed class DirectoryScannerService : IDirectoryScanner
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", "node_modules"
    };

    private readonly IFileStorage _fileStorage;
    private readonly ImmutableArray<string> patterns;

    public DirectoryScannerService()
    {
        _fileStorage = ImanFileStorageFactory.CreateStorage();

        patterns = Enum.GetValues<FileType>()
            .Where(x => x != FileType.None)
            .Select(x => x.GetDescription())
            .ToImmutableArray();
    }

    public async Task<Outcome<List<DiscoveredFile>>> InvestigateDirectoryAsync(string path)
    {
        var searchOutCome = await _fileStorage.SearchFilesAsync(path, patterns, SearchOption.AllDirectories);
        if (!searchOutCome.IsSuccess || searchOutCome.Data is null)
            return Outcome.Failure<List<DiscoveredFile>>(searchOutCome.Error);

        var discovered = new List<DiscoveredFile>();
        foreach (var item in searchOutCome.Data)
        {
            if (IsInExcludedDirectory(path, item.FullPath)) continue;

            var fileType = DetermineFileType(item);
            if (fileType is FileType.None) continue;

            var readDataOutCome = await ReadFileContent(item.FullPath);

            if (readDataOutCome.IsSuccess && readDataOutCome.Data is not null)
                discovered.Add(
                    new DiscoveredFile(
                        item.FullPath,
                        fileType,
                        readDataOutCome.Data
                        )
                    );
        }

        return Outcome.Successful(discovered);
    }

    private static bool IsInExcludedDirectory(string root, string filePath)
    {
        // Only directories below the requested root are excluded, not its ancestors.
        var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(root, filePath));
        if (string.IsNullOrEmpty(relativeDirectory)) return false;

        return relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(ExcludedDirectoryNames.Contains);
    }

    private async Task<Outcome<string>> ReadFileContent(string path)
    {
        if (!_fileStorage.FileExists(path))
            return Outcome.Failure<string>(new OutcomeError(
                $"File not found: {path}",
                "FILE_NOT_FOUND",
                OutcomeErrorType.NotFound));

        return await _fileStorage.ReadAllTextAsync(path);
    }

    private static FileType DetermineFileType(FileMetadata metadata)
    {
        if (metadata.FileName.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase))
            return FileType.DirectoryPackagesProps;

        if (metadata.FileName.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase))
            return FileType.DirectoryBuildProps;

        return metadata.Extension switch
        {
            ".sln" => FileType.SolutionClassic,
            ".slnx" => FileType.SolutionXml,
            ".csproj" => FileType.Project,
            _ => FileType.None
        };
    }

}
