using POS.Domain.Shared;

namespace POS.Domain.Assessment;

public sealed class RubricBuilder
{
    private readonly Rubric _draft = new(RubricId.New());
    public IReadOnlyList<Criterion> Preview => _draft.Criteria;

    public Criterion Add(string name, decimal maxScore, decimal weight) =>
        _draft.Add(CriterionId.New(), name, maxScore, weight);

    public void Undo()
    {
        if (Preview.Count == 0)
            throw new DomainException("Черновик рубрикатора пуст.");
        _draft.Remove(Preview[^1].Id);
    }

    public Rubric Build()
    {
        if (Preview.Count == 0)
            throw new DomainException("Добавьте хотя бы один критерий.");
        var rubric = new Rubric(_draft.Id);
        foreach (var item in Preview)
            rubric.Add(item.Id, item.Name, item.MaxScore, item.Weight);
        return rubric;
    }
}
