using POS.Domain.Assessment;
using POS.Domain.Shared;
using POS.Domain.Students;

namespace POS.Domain.Services;

public enum FinalScoreStatus { NoResult, Preliminary, Final }

public sealed record FinalScoreResult(decimal? RawValue, decimal? RoundedValue,
    FinalScoreStatus Status, int GradedCriteria, int TotalCriteria);

public interface IRoundingStrategy
{
    decimal Round(decimal value);
}

public sealed class HalfUpRoundingStrategy : IRoundingStrategy
{
    public decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed class FloorRoundingStrategy : IRoundingStrategy
{
    public decimal Round(decimal value) => Math.Floor(value);
}

public sealed class FinalScoreCalculator
{
    public FinalScoreResult Calculate(StudentId student, Rubric rubric, Gradebook book, IRoundingStrategy rounding)
    {
        Rules.Id(student.Value);
        ArgumentNullException.ThrowIfNull(rounding);
        book.EnsureRubric(rubric);
        var evaluated = book.Evaluations.Where(e => e.StudentId == student)
            .Select(e => (e.Score, Criterion: rubric.Get(e.CriterionId))).ToArray();

        var total = rubric.Criteria.Count;
        if (evaluated.Length == 0)
            return new FinalScoreResult(null, null, FinalScoreStatus.NoResult, 0, total);

        decimal weightSum = 0;
        decimal scale = 1;
        foreach (var item in evaluated)
        {
            if (item.Criterion.Weight > decimal.MaxValue - weightSum)
            {
                scale = evaluated.Max(e => e.Criterion.Weight);
                break;
            }
            weightSum += item.Criterion.Weight;
        }
        var denominator = scale == 1 ? weightSum : evaluated.Sum(e => e.Criterion.Weight / scale);
        var numerator = evaluated.Sum(e =>
            (e.Criterion.Weight / scale) * (e.Score.Value / e.Criterion.MaxScore));
        var raw = Math.Min(10m, numerator / denominator * 10m);
        var rounded = rounding.Round(raw);
        if (rounded is < 0 or > 10)
            throw new DomainException("Результат округления должен быть в диапазоне от 0 до 10.");
        var status = evaluated.Length == total ? FinalScoreStatus.Final : FinalScoreStatus.Preliminary;
        return new FinalScoreResult(raw, rounded, status, evaluated.Length, total);
    }
}
