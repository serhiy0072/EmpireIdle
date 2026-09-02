using AwesomeAssertions;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Тести живуть у tests, не в src.
///
/// Файл тесту в продакшн-проєкті не збереться — там немає xUnit. Небезпека
/// в тому, як це «лагодять»: додають тестові пакети в src, і вони їдуть
/// у прод разом із застосунком.
/// </summary>
public class TestPlacementTests
{
    [Fact]
    public void TestFiles_ShouldNotLiveInProductionProjects()
    {
        var root = FindRepositoryRoot();

        var misplaced = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
            .Where(f => !f.Replace('\\', '/').Contains("/bin/", StringComparison.Ordinal))
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .EndsWith("Tests", StringComparison.Ordinal))
            .Select(f => f.Replace('\\', '/')[root.Length..])
            .ToList();

        misplaced.Should().BeEmpty(
            "тести належать проєктам у tests/. Знайдено в src:\n" + string.Join('\n', misplaced));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EmpireIdle.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("EmpireIdle.slnx not found above the test output directory.");
    }
}
