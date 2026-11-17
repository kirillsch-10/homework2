using System.Collections.ObjectModel;
using POS.Domain.Shared;

namespace POS.Domain.Assessment;

public sealed class Criterion
{
    public CriterionId Id { get; }
    public string Name { get; }
    public decimal MaxScore { get; }
    public decimal Weight { get; }

    internal Criterion(CriterionId id, string name, decimal maxScore, decimal weight)
    {
        Rules.Id(id.Value);
        Id = id;
        Name = Rules.Text(name, 120, "Название критерия");
        MaxScore = Rules.Positive(maxScore, "Максимум");
        Weight = Rules.Positive(weight, "Вес");
    }
}

public sealed class Rubric
{
    private readonly List<Criterion> _criteria = [];
    private readonly ReadOnlyCollection<Criterion> _view;

    public RubricId Id { get; }
    public IReadOnlyList<Criterion> Criteria => _view;

    public Rubric(RubricId id)
    {
        Rules.Id(id.Value);
        Id = id;
        _view = _criteria.AsReadOnly();
    }

    public Criterion Get(CriterionId id)
    {
        Rules.Id(id.Value);
        return _criteria.Find(c => c.Id == id)
            ?? throw new DomainException("Критерий отсутствует в рубрикаторе.", ErrorCode.NotFound);
    }

    public Criterion Add(CriterionId id, string name, decimal maxScore, decimal weight)
    {
        var criterion = new Criterion(id, name, maxScore, weight);
        if (_criteria.Exists(c => c.Id == id))
            throw new DomainException("Идентификатор критерия уже используется.");
        ValidateName(criterion);
        _criteria.Add(criterion);
        return criterion;
    }

    public Criterion Rename(CriterionId id, string name)
    {
        var current = Get(id);
        return Replace(current, name, current.MaxScore, current.Weight);
    }

    public Criterion ChangeMax(CriterionId id, decimal maxScore)
    {
        var current = Get(id);
        return Replace(current, current.Name, maxScore, current.Weight);
    }

    public Criterion ChangeWeight(CriterionId id, decimal weight)
    {
        var current = Get(id);
        return Replace(current, current.Name, current.MaxScore, weight);
    }

    public void Remove(CriterionId id) => _criteria.Remove(Get(id));

    private Criterion Replace(Criterion current, string name, decimal maxScore, decimal weight)
    {
        var updated = new Criterion(current.Id, name, maxScore, weight);
        ValidateName(updated);
        _criteria[_criteria.IndexOf(current)] = updated;
        return updated;
    }

    private void ValidateName(Criterion criterion)
    {
        if (_criteria.Exists(c => c.Id != criterion.Id &&
            StringComparer.OrdinalIgnoreCase.Equals(c.Name, criterion.Name)))
            throw new DomainException("Критерий с таким названием уже существует.");
    }
}
