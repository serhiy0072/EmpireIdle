using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Храповик під міграцію на TimeProvider.
///
/// Зараз у проєкті 60 прямих DateTime.UtcNow — виправити їх одним комітом
/// нереально, а мігрувати без захисту означає, що нові файли додаватимуть нові.
/// Тест фіксує стелю й опускає її з кожним зрізом: Villages → Garrisons →
/// Marches → Quests → Payments. Число тільки зменшується.
///
/// Коли дійде до нуля — замінити на Should().Be(0) і видалити лічильник.
/// </summary>
public class ClockDisciplineTests
{
    /// <summary>
    /// Стеля. ЗМЕНШУВАТИ при кожному зрізі міграції, ніколи не збільшувати.
    /// Останнє вимірювання: 60. Зріз Villages лічильник не зрушив — виклики
    /// переїхали з домену в хендлери. Наступний зріз: доменні події (−10).
    /// </summary>
    private const int AllowedDirectClockCalls = 0;

    private static readonly Regex DirectClockCall =
        new(@"\bDateTime\s*\.\s*(UtcNow|Now|Today)\b", RegexOptions.Compiled);

    [Fact]
    public void DirectClockCalls_ShouldNotGrow()
    {
        var offenders = ScanSources();

        offenders.Count.Should().BeLessThanOrEqualTo(AllowedDirectClockCalls,
            $"нові прямі виклики годинника заборонені. Знайдено {offenders.Count}, " +
            $"стеля {AllowedDirectClockCalls}. Використай TimeProvider:\n" +
            string.Join('\n', offenders.Take(15)));
    }

    [Fact]
    public void Ceiling_ShouldBeTightened_WhenMigrationProgresses()
    {
        // Ловить забуте оновлення константи: якщо зріз мігрували, а стелю не опустили,
        // наступний файл знову зможе додати UtcNow непоміченим
        var actual = ScanSources().Count;

        actual.Should().BeGreaterThan(AllowedDirectClockCalls - 5,
            $"фактичних викликів {actual} при стелі {AllowedDirectClockCalls} — " +
            "опусти AllowedDirectClockCalls до фактичного значення");
    }

    [Fact]
    public void ApiControllers_ShouldNotReadTheClock()
    {
        // Контролер, який знає котра година, майже завжди рахує щось,
        // що мало б рахуватись у хендлері
        var offenders = ScanSources()
            .Where(o => o.Contains("/Controllers/", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "час читає хендлер, не транспортний шар:\n" + string.Join('\n', offenders));
    }

    private static List<string> ScanSources()
    {
        var root = FindRepositoryRoot();
        var srcDirectory = Path.Combine(root, "src");

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var normalised = file.Replace('\\', '/');

            // Міграції генерує EF, обжʼєкти й біни — не наш код
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
