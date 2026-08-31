using System.Text.Json;

internal sealed class HotkeyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string path;
    private readonly Dictionary<OverlayCommand, HotkeyGesture> bindings = [];

    internal HotkeyStore(string? path = null)
    {
        this.path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RA2 Toolkit", "hotkeys.json");
        Load();
    }

    internal event Action? Changed;

    internal IReadOnlyDictionary<OverlayCommand, HotkeyGesture> Bindings => bindings;

    internal bool TryGet(OverlayCommand command, out HotkeyGesture gesture) =>
        bindings.TryGetValue(command, out gesture);

    internal OverlayCommand? FindConflict(
        OverlayCommand command, HotkeyGesture candidate) =>
        bindings.Where(entry => entry.Key != command && entry.Value == candidate)
            .Select(entry => (OverlayCommand?)entry.Key)
            .FirstOrDefault();

    internal void Set(OverlayCommand command, HotkeyGesture gesture)
    {
        if (!gesture.IsValid)
            throw new ArgumentOutOfRangeException(nameof(gesture));
        bindings[command] = gesture with
        {
            Modifiers = gesture.Modifiers & HotkeyGesture.SupportedModifiers
        };
        Save();
        Changed?.Invoke();
    }

    internal bool Remove(OverlayCommand command)
    {
        if (!bindings.Remove(command))
            return false;
        Save();
        Changed?.Invoke();
        return true;
    }

    private void Load()
    {
        if (!File.Exists(path))
            return;

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, StoredHotkey>>(
                File.ReadAllText(path), JsonOptions);
            if (stored is null)
                return;

            foreach (var entry in stored)
            {
                var migratedName = entry.Key switch
                {
                    "ToggleCombatBoost" => nameof(OverlayCommand.ToggleOneHitKill),
                    "PromoteSelectedUnits" => nameof(OverlayCommand.ToggleEliteUnits),
                    _ => entry.Key
                };
                if (!Enum.TryParse<OverlayCommand>(migratedName, out var command) ||
                    entry.Value.Key <= 0)
                    continue;
                bindings[command] = new HotkeyGesture(
                    entry.Value.Modifiers & HotkeyGesture.SupportedModifiers,
                    entry.Value.Key);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or NotSupportedException)
        {
            // A malformed optional settings file must not prevent the controller from starting.
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("快捷键配置路径无效。");
        Directory.CreateDirectory(directory);
        var stored = bindings.ToDictionary(
            entry => entry.Key.ToString(),
            entry => new StoredHotkey(entry.Value.Modifiers, entry.Value.Key));
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private sealed record StoredHotkey(uint Modifiers, int Key);
}
