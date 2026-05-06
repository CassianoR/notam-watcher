namespace NotamWatcher.Parsing.Models;

/// <summary>
/// Discriminated-union result type. Keeps the parser's public API honest about failures
/// without throwing on every malformed field.
/// </summary>
public abstract record ParseResult<T>
{
    public sealed record Ok(T Value) : ParseResult<T>;
    public sealed record Fail(string Reason) : ParseResult<T>;

    public bool IsOk => this is Ok;
    public T? ValueOrDefault => this is Ok ok ? ok.Value : default;

    public ParseResult<TOut> Map<TOut>(Func<T, TOut> f) =>
        this is Ok ok ? new ParseResult<TOut>.Ok(f(ok.Value)) : new ParseResult<TOut>.Fail(((Fail)this).Reason);
}
