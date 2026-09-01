internal sealed class OverlayCommandDispatcher
{
    private readonly IReadOnlyDictionary<OverlayCommand, Func<OverlayCommandRequest, int?>> handlers;

    internal OverlayCommandDispatcher(
        IReadOnlyDictionary<OverlayCommand, Func<OverlayCommandRequest, int?>> handlers)
    {
        var missing = Enum.GetValues<OverlayCommand>()
            .Where(command => !handlers.ContainsKey(command))
            .ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException(
                $"命令未注册：{string.Join(", ", missing)}");

        this.handlers = handlers;
    }

    internal int? Execute(OverlayCommandRequest request) =>
        handlers.TryGetValue(request.Command, out var handler) ? handler(request) : null;
}
