using System.Text.Json;
using POS.Application.Contracts;
using POS.Domain.Assessment;
using POS.Domain.Students;

namespace POS.Infrastructure.Persistence;

internal static class PosFileMapper
{
    private const int Version = 1;

    public static PosSnapshot Restore(PosFileDto dto)
    {
        if (dto.SchemaVersion != Version)
            throw new JsonException($"Неподдерживаемая версия схемы ({dto.SchemaVersion}).");

        var students = Items(dto.Students).Select(item => new Student(new StudentId(item.Id),
            new FullName(item.FirstName, item.LastName, item.MiddleName), new StudyYear(item.StudyYear),
            item.EducationalProgram, item.AcademicGroup)).ToArray();

        if (dto.Rubric is null && dto.Gradebook is null)
            return new PosSnapshot(students);
        if (dto.Rubric is null || dto.Gradebook is null)
            throw new JsonException("Рубрикатор и журнал должны существовать вместе.");

        var rubric = new Rubric(new RubricId(dto.Rubric.Id));
        foreach (var item in Items(dto.Rubric.Criteria))
            rubric.Add(new CriterionId(item.Id), item.Name, item.MaxScore, item.Weight);

        var book = new Gradebook(new RubricId(dto.Gradebook.RubricId));
        book.EnsureRubric(rubric);
        foreach (var item in Items(dto.Gradebook.Evaluations))
            book.Record(rubric, new StudentId(item.StudentId), new CriterionId(item.CriterionId), new Score(item.Score));
        return new PosSnapshot(students, rubric, book);
    }

    public static PosFileDto Capture(PosSnapshot snapshot)
    {
        snapshot.Validate();
        var students = snapshot.Students.Select(s => new StudentFileDto(s.Id.Value,
            s.Name.FirstName, s.Name.LastName, s.Name.MiddleName, s.Year.Value,
            s.EducationalProgram, s.AcademicGroup)).ToArray();
        var rubric = snapshot.Rubric is { } current
            ? new RubricFileDto(current.Id.Value, current.Criteria.Select(c =>
                new CriterionFileDto(c.Id.Value, c.Name, c.MaxScore, c.Weight)).ToArray()) : null;
        var book = snapshot.Gradebook is { } gradebook
            ? new GradebookFileDto(gradebook.RubricId.Value, gradebook.Evaluations.Select(e =>
                new EvaluationFileDto(e.StudentId.Value, e.CriterionId.Value, e.Score.Value)).ToArray()) : null;
        return new PosFileDto(Version, students, rubric, book);
    }

    private static IEnumerable<T> Items<T>(T[]? source) where T : class
    {
        if (source is null)
            throw new JsonException("Ожидался массив записей.");
        foreach (var item in source)
            yield return item ?? throw new JsonException("Пустая запись в массиве.");
    }
}
