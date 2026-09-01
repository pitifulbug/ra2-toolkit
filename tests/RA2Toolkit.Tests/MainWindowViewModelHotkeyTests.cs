using Xunit;

public sealed class MainWindowViewModelHotkeyTests
{
    [Fact]
    public void Captured_global_hotkey_is_saved_without_triggering_the_feature()
    {
        var path = CreateHotkeyPath();
        try
        {
            var requests = new List<OverlayCommandRequest>();
            var store = new HotkeyStore(path);
            using var viewModel = CreateViewModel(store, requests.Add);
            var feature = ConnectAndGetFeature(viewModel, OverlayCommand.ToggleRevealMap);
            var gesture = new HotkeyGesture(HotkeyGesture.Control, 75);

            feature.CaptureHotkeyCommand.Execute(null);
            viewModel.HandleCapturedHotkey(gesture);

            Assert.False(viewModel.IsCapturingHotkey);
            Assert.True(store.TryGet(OverlayCommand.ToggleRevealMap, out var saved));
            Assert.Equal(gesture, saved);
            Assert.Empty(requests);

            viewModel.HandleGlobalHotkey(gesture);
            Assert.Equal(OverlayCommand.ToggleRevealMap, Assert.Single(requests).Command);
        }
        finally
        {
            DeleteHotkeyFile(path);
        }
    }

    [Fact]
    public void Captured_escape_cancels_and_delete_clears_the_binding()
    {
        var path = CreateHotkeyPath();
        try
        {
            var store = new HotkeyStore(path);
            var original = new HotkeyGesture(0, 75);
            store.Set(OverlayCommand.ToggleRevealMap, original);
            using var viewModel = CreateViewModel(store, _ => { });
            var feature = ConnectAndGetFeature(viewModel, OverlayCommand.ToggleRevealMap);

            feature.CaptureHotkeyCommand.Execute(null);
            viewModel.HandleCapturedHotkey(new HotkeyGesture(0, 0x1B));

            Assert.False(viewModel.IsCapturingHotkey);
            Assert.True(store.TryGet(OverlayCommand.ToggleRevealMap, out var unchanged));
            Assert.Equal(original, unchanged);

            feature.CaptureHotkeyCommand.Execute(null);
            viewModel.HandleCapturedHotkey(new HotkeyGesture(0, 0x2E));

            Assert.False(viewModel.IsCapturingHotkey);
            Assert.False(store.TryGet(OverlayCommand.ToggleRevealMap, out _));
        }
        finally
        {
            DeleteHotkeyFile(path);
        }
    }

    private static MainWindowViewModel CreateViewModel(
        HotkeyStore store,
        Action<OverlayCommandRequest> dispatch) =>
        new(dispatch, store, new StubUpdateService());

    private static FeatureItemViewModel ConnectAndGetFeature(
        MainWindowViewModel viewModel,
        OverlayCommand command)
    {
        viewModel.ApplyState(new OverlayState
        {
            Connected = true,
            RestrictedFeaturesAvailable = true
        });
        return viewModel.Features[command.ToString()];
    }

    private static string CreateHotkeyPath() => Path.Combine(
        Path.GetTempPath(), $"ra2-toolkit-hotkeys-{Guid.NewGuid():N}.json");

    private static void DeleteHotkeyFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed class StubUpdateService : IUpdateService
    {
        public Task<UpdateCheckResult> CheckAsync(
            Version currentVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(new UpdateCheckResult(
                false, currentVersion, new Uri("https://example.com/release")));
    }
}
