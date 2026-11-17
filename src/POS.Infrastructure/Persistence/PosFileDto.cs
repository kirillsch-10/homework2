using System.Text.Json.Serialization;

namespace POS.Infrastructure.Persistence;

internal sealed record PosFileDto(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] StudentFileDto[] Students,
    [property: JsonRequired] RubricFileDto? Rubric,
    [property: JsonRequired] GradebookFileDto? Gradebook);

internal sealed record StudentFileDto(
    Guid Id, string FirstName, string LastName, string? MiddleName,
    int StudyYear, string EducationalProgram, string AcademicGroup);

internal sealed record RubricFileDto(Guid Id, CriterionFileDto[] Criteria);
internal sealed record CriterionFileDto(Guid Id, string Name, decimal MaxScore, decimal Weight);
internal sealed record GradebookFileDto(Guid RubricId, EvaluationFileDto[] Evaluations);
internal sealed record EvaluationFileDto(Guid StudentId, Guid CriterionId, [property: JsonRequired] decimal Score);
