using Xunit;
using System.Text.RegularExpressions;

public sealed class FeatureCatalogTests
{
    [Fact]
    public void Catalog_has_one_visible_entry_per_feature()
    {
        Assert.Equal(75, FeatureCatalog.All.Count);
        Assert.Equal(75, FeatureCatalog.All.Select(feature => feature.Title).Distinct().Count());
    }

    [Fact]
    public void Main_window_xaml_declares_every_feature_exactly_once()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root.FullName,
            "src", "RA2Toolkit", "UI", "Views", "MainWindow.xaml"));
        var declared = Regex.Matches(xaml, @"Features\[([A-Za-z0-9_]+)\]")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        var expected = FeatureCatalog.All
            .Select(feature => feature.PrimaryCommand.ToString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected.Count, declared.Length);
        Assert.True(expected.SetEquals(declared));
        Assert.All(declared.GroupBy(command => command),
            group => Assert.Single(group));
        Assert.Contains("SelectedIndex=\"0\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("原有工具", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Game_compatibility_does_not_use_file_hash_whitelists()
    {
        var root = FindRepositoryRoot();
        var gameDirectory = Path.Combine(root.FullName, "src", "RA2Toolkit", "Game");
        var sources = string.Join('\n', Directory
            .EnumerateFiles(gameDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("SHA256.HashData", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportedHashes", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportedAresHashes", sources, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_covers_every_overlay_command()
    {
        var expected = Enum.GetValues<OverlayCommand>().ToHashSet();

        Assert.True(expected.SetEquals(FeatureCatalog.CoveredCommands));
    }

    [Fact]
    public void Each_command_belongs_to_at_most_one_feature()
    {
        var owners = FeatureCatalog.All
            .SelectMany(feature => CommandsOf(feature).Select(command => (command, feature.Title)))
            .GroupBy(item => item.command)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Title).ToArray());

        Assert.Empty(owners);
    }

    [Theory]
    [InlineData((int)FeatureCategory.Player, 21)]
    [InlineData((int)FeatureCategory.Units, 17)]
    [InlineData((int)FeatureCategory.Miscellaneous, 10)]
    [InlineData((int)FeatureCategory.Enemy, 3)]
    [InlineData((int)FeatureCategory.Fun, 6)]
    [InlineData((int)FeatureCategory.Values, 18)]
    public void Visible_categories_have_expected_distinct_feature_counts(
        int categoryValue,
        int expectedCount)
    {
        var category = (FeatureCategory)categoryValue;
        Assert.Equal(expectedCount, FeatureCatalog.All.Count(feature => feature.Category == category));
    }

    [Fact]
    public void Parameter_controls_do_not_expose_hotkeys()
    {
        var parameters = FeatureCatalog.All
            .Where(feature => feature.ControlKind is FeatureControlKind.Number or FeatureControlKind.Choice)
            .ToArray();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, feature =>
        {
            Assert.Null(feature.ToggleCommand);
            Assert.Null(feature.HotkeyBindingCommand);
            Assert.Null(feature.HotkeyPressCommand);
            Assert.Null(feature.HotkeyDoublePressCommand);
        });
    }

    [Theory]
    [InlineData((int)OverlayCommand.ToggleSelectedInfiniteSpeed)]
    [InlineData((int)OverlayCommand.ToggleSelectedInfiniteRange)]
    [InlineData((int)OverlayCommand.ArrangeSelectedFormation)]
    [InlineData((int)OverlayCommand.ToggleSelectedSpinningMcvs)]
    [InlineData((int)OverlayCommand.ToggleSelectedCratePickers)]
    public void Selected_object_operations_use_one_action_button(int commandValue)
    {
        var command = (OverlayCommand)commandValue;
        var feature = Assert.Single(FeatureCatalog.All,
            candidate => candidate.PrimaryCommand == command);

        Assert.Equal(FeatureControlKind.Action, feature.ControlKind);
        Assert.Null(feature.ToggleCommand);
    }

    [Fact]
    public void Number_and_choice_defaults_are_valid()
    {
        var numbers = FeatureCatalog.All
            .Where(feature => feature.ControlKind == FeatureControlKind.Number)
            .ToArray();
        Assert.All(numbers, feature =>
        {
            Assert.NotNull(feature.DefaultNumber);
            Assert.NotNull(feature.MinimumNumber);
            Assert.NotNull(feature.MaximumNumber);
            Assert.NotNull(feature.NumberStep);
            Assert.InRange(feature.DefaultNumber!.Value,
                feature.MinimumNumber!.Value, feature.MaximumNumber!.Value);
            Assert.True(feature.NumberStep > 0);
        });

        var choices = FeatureCatalog.All
            .Where(feature => feature.ControlKind == FeatureControlKind.Choice)
            .ToArray();
        Assert.All(choices, feature =>
        {
            Assert.NotEmpty(feature.Choices!);
            Assert.Contains(feature.Choices!, choice => choice.Value == feature.DefaultOption);
        });
    }

    private static IEnumerable<OverlayCommand> CommandsOf(FeatureDefinition feature) =>
        new OverlayCommand?[]
        {
            feature.PrimaryCommand,
            feature.ToggleCommand,
            feature.HotkeyBindingCommand,
            feature.HotkeyPressCommand,
            feature.HotkeyDoublePressCommand
        }
        .Where(command => command is not null)
        .Select(command => command!.Value)
        .Distinct();

    private static DirectoryInfo FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[]
                 {
                     new DirectoryInfo(Path.GetDirectoryName(sourceFile)!),
                     new DirectoryInfo(Directory.GetCurrentDirectory()),
                     new DirectoryInfo(AppContext.BaseDirectory)
                 })
        {
            for (var directory = start;
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ra2-toolkit.slnx")))
                    return directory;
            }
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根目录。");
    }
}
