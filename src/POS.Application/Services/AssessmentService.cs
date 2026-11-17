using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Domain.Assessment;
using POS.Domain.Shared;
using POS.Domain.Students;

namespace POS.Application.Services;

public sealed class AssessmentService(IPosRepository repository)
{
    public async Task<RubricView?> GetAsync()
    {
        var snapshot = await repository.LoadAsync();
        return snapshot.Rubric is { } rubric ? View(rubric) : null;
    }

    public async Task<RubricView> InitializeAsync(Rubric rubric)
    {
        var snapshot = await repository.LoadAsync();
        snapshot.Initialize(rubric);
        await repository.SaveAsync(snapshot);
        return View(rubric);
    }

    public Task<CriterionView> AddAsync(CriterionSpec spec) =>
        ChangeAsync((_, rubric, _) => View(rubric.Add(CriterionId.New(), spec.Name, spec.MaxScore, spec.Weight)));

    public Task<CriterionView> RenameAsync(Guid id, string name) =>
        ChangeAsync((_, rubric, _) => View(rubric.Rename(new CriterionId(id), name)));

    public Task<CriterionView> ChangeWeightAsync(Guid id, decimal weight) =>
        ChangeAsync((_, rubric, _) => View(rubric.ChangeWeight(new CriterionId(id), weight)));

    public Task<MaxScoreChange> ChangeMaxAsync(Guid id, decimal maximum) =>
        ChangeAsync((_, rubric, book) =>
        {
            var key = new CriterionId(id);
            var criterion = rubric.ChangeMax(key, maximum);
            return new MaxScoreChange(View(criterion), book.ClampToMaximum(rubric, key));
        });

    public Task RemoveAsync(Guid id) => ChangeAsync((_, rubric, book) =>
    {
        var key = new CriterionId(id);
        if (book.Evaluations.Any(e => e.CriterionId == key))
            throw new DomainException("Сначала отмените все оценки по этому критерию.", ErrorCode.CriterionInUse);
        rubric.Remove(key);
        return true;
    });

    public Task<EvaluationView> RecordAsync(RecordEvaluationRequest request) =>
        ChangeAsync((snapshot, rubric, book) =>
        {
            var student = snapshot.Get(new StudentId(request.StudentId));
            var evaluation = book.Record(rubric, student.Id, new CriterionId(request.CriterionId), new Score(request.Score));
            return View(evaluation, rubric);
        });

    public Task<EvaluationView> CorrectAsync(RecordEvaluationRequest request) =>
        ChangeAsync((snapshot, rubric, book) =>
        {
            var student = snapshot.Get(new StudentId(request.StudentId));
            var evaluation = book.Correct(rubric, student.Id, new CriterionId(request.CriterionId), new Score(request.Score));
            return View(evaluation, rubric);
        });

    public Task CancelAsync(Guid studentId, Guid criterionId) => ChangeAsync((snapshot, rubric, book) =>
    {
        var student = snapshot.Get(new StudentId(studentId));
        book.Cancel(rubric, student.Id, new CriterionId(criterionId));
        return true;
    });

    public async Task<IReadOnlyList<EvaluationView>> ListAsync(Guid studentId)
    {
        var snapshot = await repository.LoadAsync();
        var student = snapshot.Get(new StudentId(studentId));
        if (snapshot.Rubric is null)
            return [];
        var (rubric, book) = snapshot.Assessment();
        return book.Evaluations.Where(e => e.StudentId == student.Id).Select(e => View(e, rubric))
            .OrderBy(e => e.Criterion.Data.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<T> ChangeAsync<T>(Func<PosSnapshot, Rubric, Gradebook, T> change)
    {
        var snapshot = await repository.LoadAsync();
        var (rubric, book) = snapshot.Assessment();
        var result = change(snapshot, rubric, book);
        await repository.SaveAsync(snapshot);
        return result;
    }

    internal static CriterionView View(Criterion c) => new(c.Id.Value, new CriterionSpec(c.Name, c.MaxScore, c.Weight));
    private static RubricView View(Rubric rubric) => new(rubric.Id.Value, rubric.Criteria.Select(View).ToArray());
    private static EvaluationView View(Evaluation e, Rubric rubric) =>
        new(e.StudentId.Value, View(rubric.Get(e.CriterionId)), e.Score.Value);
}
