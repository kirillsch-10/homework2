using POS.Application.Contracts;

namespace POS.Application.Ports;

public interface IPosOperations
{
    Task<StudentView> AddStudentAsync(AddStudentRequest request);
    Task<StudentView> UpdateStudentAsync(Guid id, AddStudentRequest request);
    Task RemoveStudentAsync(Guid id);
    Task<IReadOnlyList<StudentView>> ListStudentsAsync(StudentFilter? filter = null);

    Task StartRubricSetupAsync();
    CriterionView AddDraftCriterion(CriterionSpec spec);
    void UndoLastDraftCriterion();
    IReadOnlyList<CriterionView> PreviewRubricDraft();
    Task<RubricView> FinishRubricSetupAsync();
    void CancelRubricSetup();
    Task<RubricView?> GetRubricAsync();
    Task<CriterionView> AddCriterionAsync(CriterionSpec spec);
    Task<CriterionView> RenameCriterionAsync(Guid id, string name);
    Task<MaxScoreChange> ChangeMaxScoreAsync(Guid id, decimal maxScore);
    Task<CriterionView> ChangeWeightAsync(Guid id, decimal weight);
    Task RemoveCriterionAsync(Guid id);

    Task<EvaluationView> RecordEvaluationAsync(RecordEvaluationRequest request);
    Task<EvaluationView> CorrectEvaluationAsync(RecordEvaluationRequest request);
    Task CancelEvaluationAsync(Guid studentId, Guid criterionId);
    Task<IReadOnlyList<EvaluationView>> ListEvaluationsAsync(Guid studentId);
    Task<ResultRow> CalculateFinalAsync(Guid studentId, RoundingMode mode = RoundingMode.HalfUp);
    Task<IReadOnlyList<ResultRow>> ListResultsAsync(StudentFilter? filter = null, RoundingMode mode = RoundingMode.HalfUp);
    Task<string> ExportResultsAsync(string path, StudentFilter? filter = null, RoundingMode mode = RoundingMode.HalfUp);
}
