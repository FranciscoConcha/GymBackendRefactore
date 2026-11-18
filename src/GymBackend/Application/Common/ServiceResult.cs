namespace GymBackend.Application.Common;

public enum ServiceErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record ServiceError(ServiceErrorKind Kind, string Message)
{
    public static ServiceError Validation(string message) => new(ServiceErrorKind.Validation, message);
    public static ServiceError Unauthorized(string message) => new(ServiceErrorKind.Unauthorized, message);
    public static ServiceError Forbidden() => new(ServiceErrorKind.Forbidden, string.Empty);
    public static ServiceError Conflict(string message) => new(ServiceErrorKind.Conflict, message);
    public static ServiceError NotFound(string message) => new(ServiceErrorKind.NotFound, message);
}

/// <summary>Resultado de un caso de uso: valor o error de negocio, sin depender de HTTP.</summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ServiceResult<T> Success(T value) => new(value, null);
    public static ServiceResult<T> Failure(ServiceError error) => new(default, error);

    public static implicit operator ServiceResult<T>(ServiceError error) => Failure(error);
}
