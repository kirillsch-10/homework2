using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Application.Services;

namespace POS.Application;

public sealed class PosFacade(StudentService students, AssessmentService assessment,
    ReportingService reporting, RubricSetupSession setup) : IPosOperations
{
    public Task<StudentView> AddStudentAsync(AddStudentRequest request) => students.AddAsync(request);
    public Task<StudentView> UpdateStudentAsync(Guid id, AddStudentRequest request) => students.UpdateAsync(id, request);
    public Task RemoveStudentAsync(Guid id) => students.RemoveAsync(id);
    public Task<IReadOnlyList<StudentView>> ListStudentsAsync(StudentFilter? filter = null) => students.ListAsync(filter);

    public Task StartRubricSetupAsync() => setup.StartAsync();
    public CriterionView AddDraftCriterion(CriterionSpec spec) => setup.Add(spec);
    public void UndoLastDraftCriterion() => setup.Undo();
    public IReadOnlyList<CriterionView> PreviewRubricDraft() => setup.Preview();
    public Task<RubricView> FinishRubricSetupAsync() => setup.FinishAsync();
    public void CancelRubricSetup() => setup.Cancel();
    public Task<RubricView?> GetRubricAsync() => assessment.GetAsync();
    public Task<CriterionView> AddCriterionAsync(CriterionSpec spec) => assessment.AddAsync(spec);
    public Task<CriterionView> RenameCriterionAsync(Guid id, string name) => assessment.RenameAsync(id, name);
    public Task<MaxScoreChange> ChangeMaxScoreAsync(Guid id, decimal maxScore) => assessment.ChangeMaxAsync(id, maxScore);
    public Task<CriterionView> ChangeWeightAsync(Guid id, decimal weight) => assessment.ChangeWeightAsync(id, weight);
    public Task RemoveCriterionAsync(Guid id) => assessment.RemoveAsync(id);

    public Task<EvaluationView> RecordEvaluationAsync(RecordEvaluationRequest request) => assessment.RecordAsync(request);
    public Task<EvaluationView> CorrectEvaluationAsync(RecordEvaluationRequest request) => assessment.CorrectAsync(request);
    public Task CancelEvaluationAsync(Guid studentId, Guid criterionId) => assessment.CancelAsync(studentId, criterionId);
    public Task<IReadOnlyList<EvaluationView>> ListEvaluationsAsync(Guid studentId) => assessment.ListAsync(studentId);

    public Task<ResultRow> CalculateFinalAsync(Guid studentId, RoundingMode mode = RoundingMode.HalfUp) =>
        reporting.CalculateAsync(studentId, mode);

    public Task<IReadOnlyList<ResultRow>> ListResultsAsync(StudentFilter? filter = null, RoundingMode mode = RoundingMode.HalfUp) =>
        reporting.ListAsync(filter, mode);

    public Task<string> ExportResultsAsync(string path, StudentFilter? filter = null, RoundingMode mode = RoundingMode.HalfUp) =>
        reporting.ExportAsync(path, filter, mode);
}
