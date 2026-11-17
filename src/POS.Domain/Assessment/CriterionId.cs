using POS.Domain.Shared;

namespace POS.Domain.Assessment;

public readonly record struct CriterionId
{
    public Guid Value { get; }
    public CriterionId(Guid value) => Value = Rules.Id(value);
    public static CriterionId New() => new(Guid.NewGuid());
}
