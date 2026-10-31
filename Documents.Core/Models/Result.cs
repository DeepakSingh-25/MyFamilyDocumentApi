namespace Documents.Core.Models
{
    /// <summary>Expected failure ki kism — controller ise HTTP status mein badalta hai.</summary>
    public enum ErrorKind
    {
        None,
        Validation,   // 400
        Unauthorized, // 401
        Forbidden,    // 403
        NotFound,     // 404
        Conflict,     // 409
    }

    /// <summary>Handler ka result: success, ya ErrorKind + user ko dikhane wala message.</summary>
    public class Result
    {
        protected Result(ErrorKind kind, string? error)
        {
            Kind = kind;
            Error = error;
        }

        public ErrorKind Kind { get; }
        public string? Error { get; }
        public bool IsSuccess => Kind == ErrorKind.None;

        public static Result Success() => new(ErrorKind.None, null);
        public static Result Fail(ErrorKind kind, string error) => new(kind, error);

        public static Result<T> Success<T>(T value) => Result<T>.Ok(value);
        public static Result<T> Fail<T>(ErrorKind kind, string error) => Result<T>.Failed(kind, error);
    }

    public sealed class Result<T> : Result
    {
        private Result(ErrorKind kind, string? error, T? value) : base(kind, error) => Value = value;

        public T? Value { get; }

        internal static Result<T> Ok(T value) => new(ErrorKind.None, null, value);
        internal static Result<T> Failed(ErrorKind kind, string error) => new(kind, error, default);

        /// <summary>Kisi doosre result ki failure ko is type mein le jao.</summary>
        public static Result<T> From(Result failure) => new(failure.Kind, failure.Error, default);
    }
}
