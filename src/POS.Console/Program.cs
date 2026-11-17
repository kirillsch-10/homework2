using System.Text;
using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Application.Services;
using POS.ConsoleApp;
using POS.Domain.Services;
using POS.Infrastructure.Export;
using POS.Infrastructure.Persistence;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var dataPath = Environment.GetEnvironmentVariable("POS_DATA_PATH");
if (string.IsNullOrWhiteSpace(dataPath))
{
    var directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(directory))
        directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    dataPath = Path.Combine(directory, "POS", "pos.json");
}

try
{
    dataPath = Path.GetFullPath(dataPath);
}
catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
{
    Console.Error.WriteLine($"Проверьте путь в POS_DATA_PATH. {ex.Message}");
    return 1;
}

using var services = new ServiceCollection()
    .AddSingleton<IPosRepository>(new JsonPosRepository(dataPath))
    .AddSingleton<IResultExporter>(new CsvResultExporter(dataPath))
    .AddSingleton<FinalScoreCalculator>()
    .AddSingleton<IReadOnlyDictionary<RoundingMode, IRoundingStrategy>>(
        new Dictionary<RoundingMode, IRoundingStrategy>
        {
            [RoundingMode.HalfUp] = new HalfUpRoundingStrategy(),
            [RoundingMode.Floor] = new FloorRoundingStrategy()
        })
    .AddTransient<StudentService>()
    .AddTransient<AssessmentService>()
    .AddTransient<ReportingService>()
    .AddTransient<RubricSetupSession>()
    .AddTransient<IPosOperations, PosFacade>()
    .AddTransient<ConsoleUi>()
    .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

await services.GetRequiredService<ConsoleUi>().RunAsync();
return 0;
