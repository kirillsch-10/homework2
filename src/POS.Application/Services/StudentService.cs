using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Domain.Students;

namespace POS.Application.Services;

public sealed class StudentService(IPosRepository repository)
{
    public async Task<StudentView> AddAsync(AddStudentRequest request)
    {
        var snapshot = await repository.LoadAsync();
        var student = new Student(StudentId.New(), NameOf(request), new StudyYear(request.StudyYear),
            request.EducationalProgram, request.AcademicGroup);
        snapshot.Add(student);
        await repository.SaveAsync(snapshot);
        return View(student);
    }

    public async Task<StudentView> UpdateAsync(Guid id, AddStudentRequest request)
    {
        var snapshot = await repository.LoadAsync();
        var student = snapshot.Get(new StudentId(id));
        student.Update(NameOf(request), new StudyYear(request.StudyYear), request.EducationalProgram, request.AcademicGroup);
        await repository.SaveAsync(snapshot);
        return View(student);
    }

    public async Task RemoveAsync(Guid id)
    {
        var snapshot = await repository.LoadAsync();
        snapshot.Remove(new StudentId(id));
        await repository.SaveAsync(snapshot);
    }

    public async Task<IReadOnlyList<StudentView>> ListAsync(StudentFilter? filter) =>
        Select(await repository.LoadAsync(), filter).Select(View).ToArray();

    internal static IEnumerable<Student> Select(PosSnapshot snapshot, StudentFilter? filter)
    {
        var name = filter?.NameContains?.Trim();
        var year = filter?.StudyYear is int value ? new StudyYear(value) : null;
        return snapshot.Students.Where(s =>
                (string.IsNullOrEmpty(name) || s.Name.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)) &&
                (year is null || s.Year == year) &&
                Matches(s.EducationalProgram, filter?.EducationalProgram) &&
                Matches(s.AcademicGroup, filter?.AcademicGroup))
            .OrderBy(s => s.Name.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Name.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id.Value);
    }

    internal static StudentView View(Student s) => new(s.Id.Value,
        new AddStudentRequest(s.Name.FirstName, s.Name.LastName, s.Name.MiddleName, s.Year.Value,
            s.EducationalProgram, s.AcademicGroup));

    private static FullName NameOf(AddStudentRequest request) =>
        new(request.FirstName, request.LastName, request.MiddleName);

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || StringComparer.OrdinalIgnoreCase.Equals(value, filter.Trim());
}
