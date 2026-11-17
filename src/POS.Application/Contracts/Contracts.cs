using POS.Domain.Services;

namespace POS.Application.Contracts;

public sealed record AddStudentRequest(string FirstName, string LastName, string? MiddleName,
    int StudyYear, string EducationalProgram, string AcademicGroup);

public sealed record StudentView(Guid Id, AddStudentRequest Data)
{
    public string FullName => Data.MiddleName is null
        ? $"{Data.LastName} {Data.FirstName}" : $"{Data.LastName} {Data.FirstName} {Data.MiddleName}";
}

public sealed record StudentFilter(string? NameContains = null, string? EducationalProgram = null,
    int? StudyYear = null, string? AcademicGroup = null);

public sealed record CriterionSpec(string Name, decimal MaxScore, decimal Weight);
public sealed record CriterionView(Guid Id, CriterionSpec Data);
public sealed record RubricView(Guid Id, IReadOnlyList<CriterionView> Criteria);
public sealed record MaxScoreChange(CriterionView Criterion, int AdjustedEvaluations);
public sealed record RecordEvaluationRequest(Guid StudentId, Guid CriterionId, decimal Score);
public sealed record EvaluationView(Guid StudentId, CriterionView Criterion, decimal Score);
public sealed record ResultRow(StudentView Student, FinalScoreResult Result);
public enum RoundingMode { HalfUp, Floor }

public enum FileError { StorageError, DataCorrupted, ExportError }

public sealed class PosFileException(FileError code, string message, Exception inner) : Exception(message, inner)
{
    public FileError Code { get; } = code;
}
