using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Прямих викликів годинника в src немає.
///
/// Час приходить із TimeProvider у хендлери й сервіси, а в домен — параметром
/// методу. Причина не в чистоті: поведінка, прив'язана до DateTime.UtcNow,
/// не тестується інакше як через Thread.Sleep, а операція, що читає годинник
/// двічі, отримує два різні моменти й дає плаваючі результати.
///
/// Раніше тут був храповик зі стелею 60 — міграцію завершено, і правило
/// стало абсолютним.
/// </summary>
public class ClockDisciplineTests
{
    private static readonly Regex DirectClockCall =
        new(@"\bDateTime\s*\.\s*(UtcNow|Now|Today)\b", RegexOptions.Compiled);

    [Fact]
    public void DirectClockCalls_ShouldNotExist()
    {
        var offenders = ScanSources();

        offenders.Should().BeEmpty(
            "час береться з TimeProvider або приходить параметром. Знайдено:\n"
            + string.Join('\n', offenders));
    }

    private static List<string> ScanSources()
    {
        var root = FindRepositoryRoot();
        var srcDirectory = Path.Combine(root, "src");

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = file.Replace('\\', '/');

            // Міграції генерує EF, obj і bin — не наш код
            if (normalised.Contains("/Migrations/", StringComparison.Ordinal)
                || normalised.Contains("/obj/", StringComparison.Ordinal)
                || normalised.Contains("/bin/", StringComparison.Ordinal))
                continue;

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Коментарі не рахуємо: у XML-доках згадка UtcNow цілком доречна
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith('*'))
                    continue;

                if (DirectClockCall.IsMatch(line))
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
            ?? throw new InvalidOperationException(
                "EmpireIdle.slnx not found above the test output directory.");
    }
}
