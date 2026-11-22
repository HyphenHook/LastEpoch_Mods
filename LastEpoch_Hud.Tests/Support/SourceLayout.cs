namespace LastEpoch_Hud.Tests.Support;

/// <summary>Locate source by identity without coupling behavior tests to folder layout.</summary>
internal static class SourceLayout
{
    public static string Find(string fileName)
    {
        string[] matches = Directory
            .EnumerateFiles(GameEnvironment.ModProjectDir, fileName, SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(GameEnvironment.ModProjectDir, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "obj" or "bin"))
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Expected one source file named {fileName}; found {matches.Length}: {string.Join(", ", matches)}"
            );
    }
}
