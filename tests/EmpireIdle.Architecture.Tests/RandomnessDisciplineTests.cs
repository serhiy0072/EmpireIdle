using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Випадковість має бути відтворюваною скрізь, де результат бачить гравець.
///
/// Random.Shared не інжектується й не сідується, тому бій, роздачу лутбокса
/// чи розміщення на карті неможливо переграти при розборі скарги. Дозволений
/// рівно один виняток — генерація сіда бою, який потім зберігається у звіті.
/// </summary>
public class RandomnessDisciplineTests
{
    private static readonly Regex SharedRandom = new(@"\bRandom\s*\.\s*Shared\b", RegexOptions.Compiled);

    /// <summary>
    /// Місця, де Random.Shared доречний. Сід бою має бути непередбачуваним:
    /// інакше гравець порахує результат до відправки армії.
    /// </summary>
    private static readonly string[] Allowed =
    [
        "/Marches/Commands/CompleteMarchCommand.cs",
        "/Services/SystemRandomSource.cs"
    ];

    [Fact]
    public void SharedRandom_ShouldOnlyBeUsedWhereUnpredictabilityIsThePoint()
    {
        var offenders = ScanSources();

        offenders.Should().BeEmpty(
            "випадковість має сідуватись, щоб результат можна було переграти. Знайдено:\n"
            + string.Join('\n', offenders));
    }

    private static List<string> ScanSources()
    {
        var root = FindRepositoryRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var normalised = file.Replace('\\', '/');

            if (normalised.Contains("/Migrations/", StringComparison.Ordinal)
                || normalised.Contains("/obj/", StringComparison.Ordinal)
                || normalised.Contains("/bin/", StringComparison.Ordinal))
                continue;

            if (Allowed.Any(a => normalised.EndsWith(a, StringComparison.Ordinal)))
                continue;

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
                    continue;

                if (SharedRandom.IsMatch(lines[i]))
                    offenders.Add($"{normalised[root.Length..]}:{i + 1}");
            }
        }

        return offenders;
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
