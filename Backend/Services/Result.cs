namespace Backend.Services;

// What a service call did, said in a way both callers can map: the API turns it
// into a status code, the console into a message on screen. The rule itself is
// decided once, in the service — only the phrasing differs by caller.
public enum ResultKind
{
    Ok,
    NotFound,
    Forbidden,

    // A field the caller supplied is wrong in a way only the service could know
    // (a taken slug, a parent comment on another post). Shape validation belongs
    // in the request validator, not here.
    Invalid,
}

public readonly record struct Result<T>(ResultKind Kind, T? Value, string? Field, string? Message)
{
    public bool IsOk => Kind is ResultKind.Ok;

    public static Result<T> Ok(T value) => new(ResultKind.Ok, value, null, null);
    public static Result<T> NotFound() => new(ResultKind.NotFound, default, null, null);
    public static Result<T> Forbidden() => new(ResultKind.Forbidden, default, null, null);
    public static Result<T> Invalid(string field, string message) => new(ResultKind.Invalid, default, field, message);
}

// The same, for calls with nothing to hand back.
public readonly record struct Result(ResultKind Kind, string? Field, string? Message)
{
    public bool IsOk => Kind is ResultKind.Ok;

    public static Result Ok() => new(ResultKind.Ok, null, null);
    public static Result NotFound() => new(ResultKind.NotFound, null, null);
    public static Result Forbidden() => new(ResultKind.Forbidden, null, null);
    public static Result Invalid(string field, string message) => new(ResultKind.Invalid, field, message);
}
