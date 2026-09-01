using Xunit;

public sealed class OverlayCommandDispatcherTests
{
    [Fact]
    public void Constructor_rejects_a_missing_handler()
    {
        var missing = OverlayCommand.SetMoneyAmount;
        var handlers = Enum.GetValues<OverlayCommand>()
            .Where(command => command != missing)
            .ToDictionary(command => command, _ => new Func<OverlayCommandRequest, int?>(_ => 1));

        var error = Assert.Throws<InvalidOperationException>(
            () => new OverlayCommandDispatcher(handlers));

        Assert.Contains(missing.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_passes_the_complete_request_to_the_registered_handler()
    {
        OverlayCommandRequest? received = null;
        var selected = OverlayCommand.SetParadropInfantryType;
        var handlers = Enum.GetValues<OverlayCommand>()
            .ToDictionary(command => command,
                command => new Func<OverlayCommandRequest, int?>(request =>
                {
                    if (command == selected)
                        received = request;
                    return command == selected ? 7 : 1;
                }));
        var dispatcher = new OverlayCommandDispatcher(handlers);
        var request = new OverlayCommandRequest(selected, 12.5m, "INIT");

        var result = dispatcher.Execute(request);

        Assert.Equal(7, result);
        Assert.Same(request, received);
        Assert.Equal(12.5m, received!.Number);
        Assert.Equal("INIT", received.Option);
    }
}
