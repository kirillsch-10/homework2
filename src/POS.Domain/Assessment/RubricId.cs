using POS.Domain.Shared;

namespace POS.Domain.Assessment;

public readonly record struct RubricId
{
    public Guid Value { get; }
    public RubricId(Guid value) => Value = Rules.Id(value);
    public static RubricId New() => new(Guid.NewGuid());
}
