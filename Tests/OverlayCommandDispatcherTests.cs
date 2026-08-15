public sealed class OverlayCommandDispatcherTests
{
    [Fact]
    public void Constructor_RejectsMissingCommandRegistration()
    {
        var handlers = CreateHandlers();
        handlers.Remove(OverlayCommand.ExitProgram);

        var error = Assert.Throws<InvalidOperationException>(
            () => new OverlayCommandDispatcher(handlers));

        Assert.Contains(nameof(OverlayCommand.ExitProgram), error.Message);
    }

    [Fact]
    public void Execute_InvokesRegisteredHandlerAndReturnsItsResult()
    {
        var calls = 0;
        var handlers = CreateHandlers();
        handlers[OverlayCommand.ExitProgram] = () =>
        {
            calls++;
            return 42;
        };
        var dispatcher = new OverlayCommandDispatcher(handlers);

        var result = dispatcher.Execute(OverlayCommand.ExitProgram);

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Execute_ReturnsNullForUnknownCommandValue()
    {
        var dispatcher = new OverlayCommandDispatcher(CreateHandlers());

        var result = dispatcher.Execute((OverlayCommand)int.MaxValue);

        Assert.Null(result);
    }

    private static Dictionary<OverlayCommand, Func<int?>> CreateHandlers() =>
        Enum.GetValues<OverlayCommand>()
            .ToDictionary(command => command, _ => (Func<int?>)(() => null));
}
