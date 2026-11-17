namespace POS.Domain.Shared;

public enum ErrorCode
{
    Validation, DuplicateEvaluation, NotFound, CriterionInUse, StudentInUse, RubricNotInitialized
}

public sealed class DomainException(string message, ErrorCode code = ErrorCode.Validation) : Exception(message)
{
    public ErrorCode Code { get; } = code;
}

internal static class Rules
{
    public static Guid Id(Guid value) => value != Guid.Empty
        ? value : throw new DomainException("Идентификатор должен быть непустым.");

    public static decimal Positive(decimal value, string field) => value > 0
        ? value : throw new DomainException($"Поле «{field}» должно быть больше нуля.");

    public static string Text(string? value, int max, string field)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Length > max || text.Any(char.IsControl))
            throw new DomainException($"Поле «{field}» должно содержать от 1 до {max} символов без управляющих знаков.");
        return text;
    }
}
