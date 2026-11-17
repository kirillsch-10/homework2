using POS.Domain.Shared;
using POS.Domain.Students;

namespace POS.Domain.Assessment;

public readonly record struct Score
{
    public decimal Value { get; }
    public Score(decimal value) => Value = value >= 0
        ? value : throw new DomainException("Балл не может быть отрицательным.");
}

public sealed class Evaluation
{
    public StudentId StudentId { get; }
    public CriterionId CriterionId { get; }
    public Score Score { get; private set; }

    internal Evaluation(StudentId studentId, CriterionId criterionId, Score score)
    {
        StudentId = studentId;
        CriterionId = criterionId;
        Score = score;
    }

    internal void SetScore(Score score) => Score = score;
}

public sealed class Gradebook
{
    private readonly Dictionary<(StudentId, CriterionId), Evaluation> _entries = [];

    public RubricId RubricId { get; }
    public IReadOnlyCollection<Evaluation> Evaluations => _entries.Values;

    public Gradebook(RubricId rubricId)
    {
        Rules.Id(rubricId.Value);
        RubricId = rubricId;
    }

    public void EnsureRubric(Rubric rubric)
    {
        if (rubric.Id != RubricId)
            throw new DomainException("Журнал относится к другому рубрикатору.");
    }

    public Evaluation Record(Rubric rubric, StudentId student, CriterionId criterion, Score score)
    {
        CheckScore(rubric, student, criterion, score);
        var evaluation = new Evaluation(student, criterion, score);
        if (!_entries.TryAdd((student, criterion), evaluation))
            throw new DomainException("Оценка уже выставлена. Используйте исправление.", ErrorCode.DuplicateEvaluation);
        return evaluation;
    }

    public Evaluation Correct(Rubric rubric, StudentId student, CriterionId criterion, Score score)
    {
        CheckScore(rubric, student, criterion, score);
        if (!_entries.TryGetValue((student, criterion), out var evaluation))
            throw Missing();
        evaluation.SetScore(score);
        return evaluation;
    }

    public void Cancel(Rubric rubric, StudentId student, CriterionId criterion)
    {
        EnsureRubric(rubric);
        Rules.Id(student.Value);
        rubric.Get(criterion);
        if (!_entries.Remove((student, criterion)))
            throw Missing();
    }

    public int ClampToMaximum(Rubric rubric, CriterionId criterion)
    {
        EnsureRubric(rubric);
        var maximum = rubric.Get(criterion).MaxScore;
        var count = 0;
        foreach (var entry in Evaluations.Where(e => e.CriterionId == criterion && e.Score.Value > maximum))
        {
            entry.SetScore(new Score(maximum));
            count++;
        }
        return count;
    }

    private void CheckScore(Rubric rubric, StudentId student, CriterionId criterion, Score score)
    {
        EnsureRubric(rubric);
        Rules.Id(student.Value);
        var maximum = rubric.Get(criterion).MaxScore;
        if (score.Value > maximum)
            throw new DomainException($"Балл превышает максимум критерия ({maximum}).");
    }

    private static DomainException Missing() =>
        new("Оценка по этому критерию ещё не выставлена.", ErrorCode.NotFound);
}
