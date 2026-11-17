using POS.Domain.Assessment;
using POS.Domain.Shared;
using POS.Domain.Students;

namespace POS.Application.Contracts;

public sealed class PosSnapshot
{
    private readonly Dictionary<StudentId, Student> _students = [];
    public IReadOnlyCollection<Student> Students => _students.Values;
    public Rubric? Rubric { get; private set; }
    public Gradebook? Gradebook { get; private set; }

    public PosSnapshot(IEnumerable<Student>? students = null, Rubric? rubric = null, Gradebook? gradebook = null)
    {
        foreach (var student in students ?? [])
            Add(student);
        Rubric = rubric;
        Gradebook = gradebook;
        Validate();
    }

    internal void Add(Student student)
    {
        if (!_students.TryAdd(student.Id, student))
            throw new DomainException("Идентификатор студента уже используется.");
    }

    internal Student Get(StudentId id) => _students.GetValueOrDefault(id)
        ?? throw new DomainException("Студент не найден.", ErrorCode.NotFound);

    internal void Remove(StudentId id)
    {
        Get(id);
        if (Gradebook?.Evaluations.Any(e => e.StudentId == id) == true)
            throw new DomainException("Сначала отмените все оценки студента.", ErrorCode.StudentInUse);
        _students.Remove(id);
    }

    internal void Initialize(Rubric rubric)
    {
        if (Rubric is not null)
            throw new DomainException("Рубрикатор уже настроен.");
        if (rubric.Criteria.Count == 0)
            throw new DomainException("Добавьте хотя бы один критерий.");
        Rubric = rubric;
        Gradebook = new Gradebook(rubric.Id);
    }

    internal (Rubric Rubric, Gradebook Book) Assessment() =>
        Rubric is { } rubric && Gradebook is { } book
            ? (rubric, book)
            : throw new DomainException("Сначала настройте рубрикатор.", ErrorCode.RubricNotInitialized);

    public void Validate()
    {
        if (Rubric is null && Gradebook is null)
            return;
        var (rubric, book) = Assessment();
        book.EnsureRubric(rubric);
        foreach (var entry in book.Evaluations)
        {
            Get(entry.StudentId);
            if (entry.Score.Value > rubric.Get(entry.CriterionId).MaxScore)
                throw new DomainException("Сохранённый балл превышает максимум критерия.");
        }
    }
}
