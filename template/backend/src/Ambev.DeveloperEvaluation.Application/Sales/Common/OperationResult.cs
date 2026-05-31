namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public enum NotificationType
{
    Validation,
    NotFound,
    BusinessRule,
    Conflict
}

public sealed record Notification(NotificationType Type, string Message);

public sealed class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public IReadOnlyCollection<Notification> Notifications { get; }

    private OperationResult(bool isSuccess, T? data, IReadOnlyCollection<Notification> notifications)
    {
        IsSuccess = isSuccess;
        Data = data;
        Notifications = notifications;
    }

    public static OperationResult<T> Success(T data) =>
        new(true, data, []);

    public static OperationResult<T> Failure(params Notification[] notifications) =>
        new(false, default, notifications);

    public static OperationResult<T> FailureValidation(IEnumerable<string> errors) =>
        new(false, default, errors.Select(error => new Notification(NotificationType.Validation, error)).ToList());

    public static OperationResult<T> FailureNotFound(string message) =>
        new(false, default, [new Notification(NotificationType.NotFound, message)]);

    public static OperationResult<T> FailureBusinessRule(string message) =>
        new(false, default, [new Notification(NotificationType.BusinessRule, message)]);

    public static OperationResult<T> FailureConflict(string message) =>
        new(false, default, [new Notification(NotificationType.Conflict, message)]);
}
