using POS.Application.Contracts;
using POS.Domain.Assessment;
using POS.Domain.Shared;

namespace POS.Application.Services;

public sealed class RubricSetupSession(AssessmentService assessment)
{
    private RubricBuilder? _builder;
    private RubricBuilder Builder => _builder ?? throw new DomainException("Откройте черновик рубрикатора.");

    public async Task StartAsync()
    {
        if (_builder is not null)
            throw new DomainException("Черновик уже открыт.");
        if (await assessment.GetAsync() is not null)
            throw new DomainException("Рубрикатор уже настроен.");
        _builder = new RubricBuilder();
    }

    public CriterionView Add(CriterionSpec spec) => AssessmentService.View(Builder.Add(spec.Name, spec.MaxScore, spec.Weight));
    public IReadOnlyList<CriterionView> Preview() => Builder.Preview.Select(AssessmentService.View).ToArray();
    public void Undo() => Builder.Undo();
    public void Cancel() => _builder = null;

    public async Task<RubricView> FinishAsync()
    {
        var result = await assessment.InitializeAsync(Builder.Build());
        _builder = null;
        return result;
    }
}
