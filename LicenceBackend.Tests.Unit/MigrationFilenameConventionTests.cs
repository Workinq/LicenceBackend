using System.Text.RegularExpressions;

namespace LicenceBackend.Tests.Unit;

public sealed partial class MigrationFilenameConventionTests
{
    private static readonly Regex Pattern = MyRegex();

    [Fact]
    public void Migration_filenames_match_NNN_snake_case_sql_pattern()
    {
        var dir = FindMigrationsDirectory();
        var files = Directory.GetFiles(dir, "*.sql").Select(Path.GetFileName).ToList();

        Assert.NotEmpty(files);
        foreach (var name in files) Assert.Matches(Pattern, name!);
    }

    [Fact]
    public void Migration_sequence_is_dense_and_starts_at_001()
    {
        var dir = FindMigrationsDirectory();
        var nums = Directory.GetFiles(dir, "*.sql")
            .Select(p => Path.GetFileName(p))
            .Select(n => int.Parse(n.Substring(0, 3)))
            .OrderBy(n => n)
            .ToList();

        Assert.NotEmpty(nums);
        for (var i = 0; i < nums.Count; i++) Assert.Equal(i + 1, nums[i]);
    }

    private static string FindMigrationsDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LicenceBackend.sln")))
                return Path.Combine(dir.FullName, "migrations");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate repo root (LicenceBackend.sln) by walking up from '{Directory.GetCurrentDirectory()}'.");
    }

    [GeneratedRegex(@"^\d{3}_[a-z0-9_]+\.sql$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
