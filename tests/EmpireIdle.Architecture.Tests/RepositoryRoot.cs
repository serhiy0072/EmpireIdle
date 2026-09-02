namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Пошук кореня репо від вихідної теки тестів.
///
/// Архітектурні тести читають вихідний код, а не збірки — інакше вони
/// перевіряли б результат компіляції замість того, що написано.
/// Тому їм потрібен шлях до src, і саме він тут обчислюється.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>Тека, у якій лежить файл рішення.</summary>
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EmpireIdle.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "EmpireIdle.slnx not found above the test output directory. "
                + "Architecture tests need the repository root to read source files.");
    }

    /// <summary>
    /// Файли .cs у src, окрім згенерованих і зібраних.
    /// Спільний фільтр: інакше кожен тест вирішував би сам, що ігнорувати.
    /// </summary>
    public static IEnumerable<string> SourceFiles(string root)
        => Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Replace('\\', '/'))
            .Where(f => !f.Contains("/Migrations/", StringComparison.Ordinal)
                        && !f.Contains("/obj/", StringComparison.Ordinal)
                        && !f.Contains("/bin/", StringComparison.Ordinal));
}
