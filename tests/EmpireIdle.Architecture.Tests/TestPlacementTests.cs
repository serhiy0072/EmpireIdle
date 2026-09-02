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
        var root = RepositoryRoot.Find();

        var misplaced = RepositoryRoot.SourceFiles(root)
            .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("Tests", StringComparison.Ordinal))
            .Select(f => f[root.Length..])
            .ToList();

        misplaced.Should().BeEmpty(
            "тести належать проєктам у tests/. Знайдено в src:\n" + string.Join('\n', misplaced));
    }
}
