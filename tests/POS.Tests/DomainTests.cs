using POS.Domain.Assessment;
using POS.Domain.Services;
using POS.Domain.Shared;
using POS.Domain.Students;
using Xunit;

namespace POS.Tests;

public sealed class DomainTests
{
    [Theory]
    [InlineData("Анна-Мария", "Ёлкина", null)]
    [InlineData("Mary Jane", "Smith-Jones", "Rose")]
    [InlineData("Пётр", "Петров", "Петрович")]
    [InlineData(" Анна ", "Иванова", " ")]
    public void NameAcceptsBothAlphabets(string first, string last, string? middle)
    {
        var name = new FullName(first, last, middle);
        Assert.Equal(first.Trim(), name.FirstName);
        Assert.Equal(last, name.LastName);
        Assert.Equal(string.IsNullOrWhiteSpace(middle) ? null : middle, name.MiddleName);
    }

    [Theory]
    [InlineData("анна")]
    [InlineData("АннА")]
    [InlineData("Анна--Мария")]
    [InlineData("Анна  Мария")]
    [InlineData("Анна2")]
    [InlineData("Аnна")]
    [InlineData("")]
    public void NameRejectsInvalidParts(string first) =>
        Assert.Throws<DomainException>(() => new FullName(first, "Иванова"));

    [Fact]
    public void StudentAndCriteriaEnforceConstraints()
    {
        var name = new FullName("Анна", "Иванова");
        var year = new StudyYear(2);
        Assert.Throws<DomainException>(() => new StudyYear(0));
        Assert.Throws<DomainException>(() => new StudyYear(7));
        Assert.Throws<DomainException>(() => new StudentId(Guid.Empty));
        Assert.Throws<DomainException>(() => new Student(default, name, year, "ПИ", "БПИ241"));
        Assert.Throws<DomainException>(() => new Student(StudentId.New(), name, year, " ", "БПИ241"));
        Assert.Throws<DomainException>(() => new Student(StudentId.New(), name, year, "ПИ", " "));
        Assert.Throws<DomainException>(() => new Rubric(default));
        Assert.Throws<DomainException>(() => new Gradebook(default));

        var rubric = new Rubric(RubricId.New());
        var criterion = rubric.Add(CriterionId.New(), "Проект", 10, 2);
        Assert.Throws<DomainException>(() => rubric.Add(default, "Практика", 10, 1));
        Assert.Throws<DomainException>(() => rubric.Add(criterion.Id, "Практика", 10, 1));
        Assert.Throws<DomainException>(() => rubric.Add(CriterionId.New(), "  проект  ", 10, 1));
        Assert.Throws<DomainException>(() => rubric.Add(CriterionId.New(), " ", 10, 1));
        Assert.Throws<DomainException>(() => rubric.ChangeMax(criterion.Id, 0));
        Assert.Throws<DomainException>(() => rubric.ChangeWeight(criterion.Id, -1));
        Assert.Equal(10m, Assert.Single(rubric.Criteria).MaxScore);
    }

    [Fact]
    public void GradebookEnforcesMembershipScoreAndUniqueness()
    {
        var rubric = new Rubric(RubricId.New());
        var book = new Gradebook(rubric.Id);
        var criterion = rubric.Add(CriterionId.New(), "Проект", 10, 2);
        var student = StudentId.New();

        Assert.Throws<DomainException>(() => new Score(-1));
        Assert.Throws<DomainException>(() => book.Record(rubric, student, criterion.Id, new Score(12)));
        Assert.Throws<DomainException>(() => book.Record(rubric, student, CriterionId.New(), new Score(0)));
        Assert.Throws<DomainException>(() => book.Record(new Rubric(RubricId.New()), student, criterion.Id, new Score(0)));
        Assert.Throws<DomainException>(() => book.Record(rubric, default, criterion.Id, new Score(0)));
        var record = book.Record(rubric, student, criterion.Id, new Score(0));
        Assert.Equal(ErrorCode.DuplicateEvaluation, Assert.Throws<DomainException>(
            () => book.Record(rubric, student, criterion.Id, new Score(8))).Code);
        Assert.Same(record, book.Correct(rubric, student, criterion.Id, new Score(10)));
        Assert.Equal(10m, Assert.Single(book.Evaluations).Score.Value);

        book.Cancel(rubric, student, criterion.Id);
        Assert.Empty(book.Evaluations);
        Assert.Throws<DomainException>(() => book.Correct(rubric, student, criterion.Id, new Score(5)));
        Assert.Throws<DomainException>(() => book.Cancel(rubric, student, criterion.Id));
    }

    [Fact]
    public void CalculationTracksMissingZeroWeightsAndRounding()
    {
        var rubric = new Rubric(RubricId.New());
        var practice = rubric.Add(CriterionId.New(), "Практика", 20m, 2.5m);
        var project = rubric.Add(CriterionId.New(), "Проект", 10m, 1.25m);
        var student = StudentId.New();
        var book = new Gradebook(rubric.Id);
        var calculator = new FinalScoreCalculator();
        var halfUp = new HalfUpRoundingStrategy();

        var empty = calculator.Calculate(student, rubric, book, halfUp);
        Assert.Equal(FinalScoreStatus.NoResult, empty.Status);
        Assert.Null(empty.RawValue);
        book.Record(rubric, student, practice.Id, new Score(0));
        var zero = calculator.Calculate(student, rubric, book, halfUp);
        Assert.Equal(0m, zero.RawValue);
        Assert.Equal(FinalScoreStatus.Preliminary, zero.Status);

        book.Correct(rubric, student, practice.Id, new Score(7));
        Assert.Equal(4m, calculator.Calculate(student, rubric, book, halfUp).RoundedValue);
        Assert.Equal(3m, calculator.Calculate(student, rubric, book, new FloorRoundingStrategy()).RoundedValue);
        book.Record(rubric, student, project.Id, new Score(5));
        var final = calculator.Calculate(student, rubric, book, halfUp);
        Assert.Equal(4m, final.RawValue);
        Assert.Equal(FinalScoreStatus.Final, final.Status);

        rubric.ChangeWeight(project.Id, 2.5m);
        Assert.Equal(4.25m, calculator.Calculate(student, rubric, book, halfUp).RawValue);
        Assert.Equal(5m, book.Evaluations.Single(e => e.CriterionId == project.Id).Score.Value);
        rubric.Add(CriterionId.New(), "Семинар", 5, 1);
        Assert.Equal(FinalScoreStatus.Preliminary, calculator.Calculate(student, rubric, book, halfUp).Status);
    }

    [Fact]
    public void CalculationUsesOnlyGradedCriteriaAndHandlesVeryLargeWeights()
    {
        var rubric = new Rubric(RubricId.New());
        var first = rubric.Add(CriterionId.New(), "Практика", 10, decimal.MaxValue);
        var second = rubric.Add(CriterionId.New(), "Проект", 10, decimal.MaxValue);
        var third = rubric.Add(CriterionId.New(), "Семинар", 10, 1);
        var student = StudentId.New();
        var book = new Gradebook(rubric.Id);
        var calculator = new FinalScoreCalculator();
        var rounding = new HalfUpRoundingStrategy();

        book.Record(rubric, student, first.Id, new Score(8));
        book.Record(rubric, student, second.Id, new Score(6));
        var preliminary = calculator.Calculate(student, rubric, book, rounding);
        Assert.Equal(7m, preliminary.RawValue);
        Assert.Equal(2, preliminary.GradedCriteria);
        Assert.Equal(3, preliminary.TotalCriteria);
        Assert.Equal(FinalScoreStatus.Preliminary, preliminary.Status);

        book.Record(rubric, student, third.Id, new Score(10));
        Assert.Equal(FinalScoreStatus.Final, calculator.Calculate(student, rubric, book, rounding).Status);
    }
}
