using SmartDrag.Infrastructure.Preferences;
using Xunit;

namespace SmartDrag.Infrastructure.Tests;

public sealed class PreviewPreferencesStoreTests
{
    [Fact]
    public void MissingFile_ReturnsRussianLightDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        var store = new PreviewPreferencesStore(path);

        var preferences = store.Load();

        Assert.Equal("ru", preferences.Language);
        Assert.False(preferences.DarkTheme);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsNormalizedPreferences()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        var store = new PreviewPreferencesStore(path);

        Assert.True(store.TrySave(new PreviewPreferences
        {
            Language = "EN",
            DarkTheme = true,
            WindowLeft = 120,
            WindowTop = 240,
            WindowWidth = 980,
            WindowHeight = 700
        }));
        var loaded = store.Load();

        Assert.Equal("en", loaded.Language);
        Assert.True(loaded.DarkTheme);
        Assert.Equal(120, loaded.WindowLeft);
        Assert.Equal(240, loaded.WindowTop);
        Assert.Equal(980, loaded.WindowWidth);
        Assert.Equal(700, loaded.WindowHeight);
        Assert.Contains("\"language\": \"en\"", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptJson_FailsClosedToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ definitely-not-json");

        var preferences = new PreviewPreferencesStore(path).Load();

        Assert.Equal("ru", preferences.Language);
        Assert.False(preferences.DarkTheme);
    }

    [Fact]
    public void OversizedFile_FailsClosedToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', PreviewPreferencesStore.MaximumFileBytes + 1));

        var preferences = new PreviewPreferencesStore(path).Load();

        Assert.Equal("ru", preferences.Language);
        Assert.False(preferences.DarkTheme);
    }

    [Fact]
    public void UnknownLanguage_IsNormalizedToRussian()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        var store = new PreviewPreferencesStore(path);

        Assert.True(store.TrySave(new PreviewPreferences { Language = "xx", DarkTheme = false }));

        Assert.Equal("ru", store.Load().Language);
    }

    [Fact]
    public void InvalidWindowPlacement_IsDroppedDuringNormalization()
    {
        var path = Path.Combine(Path.GetTempPath(), "smartdrag-preferences", Guid.NewGuid().ToString("N"), "prefs.json");
        var store = new PreviewPreferencesStore(path);

        Assert.True(store.TrySave(new PreviewPreferences
        {
            WindowLeft = double.NaN,
            WindowTop = 200_000,
            WindowWidth = 20,
            WindowHeight = double.PositiveInfinity
        }));

        var loaded = store.Load();
        Assert.Null(loaded.WindowLeft);
        Assert.Null(loaded.WindowTop);
        Assert.Null(loaded.WindowWidth);
        Assert.Null(loaded.WindowHeight);
    }
}
