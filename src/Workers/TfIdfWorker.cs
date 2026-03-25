using Microsoft.Extensions.Options;
using TfIdfCalculator.Options;
using TfIdfCalculator.Services;

namespace TfIdfCalculator.Workers;

public class TfIdfWorker: BackgroundService
{
    private readonly IOptions<TfIdfOptions> _options;
    private readonly ILogger<TfIdfWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _env;
    private readonly TfIdfService _tfIdfService;

    public TfIdfWorker(
        IOptions<TfIdfOptions> options,
        ILogger<TfIdfWorker> logger,
        IHostApplicationLifetime lifetime,
        IHostEnvironment env,
        TfIdfService tfIdfService)
    {
        _options = options;
        _logger = logger;
        _lifetime = lifetime;
        _env = env;
        _tfIdfService = tfIdfService;
    }

    private string ResolveProjectPath(string path)
        => Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var opt = _options.Value;

            var dataPath = ResolveProjectPath(opt.DataPath);
            var lemmasFile = ResolveProjectPath(opt.LemmasFile);
            var tokensPerDocPath = ResolveProjectPath(opt.TokensPerDocPath);
            var lemmasPerDocPath = ResolveProjectPath(opt.LemmasPerDocPath);
            var outputDir = ResolveProjectPath(opt.OutputDir);

            _logger.LogInformation("Starting TF-IDF calculation...");
            _logger.LogInformation("DataPath: {Path}", dataPath);
            _logger.LogInformation("LemmasFile: {Path}", lemmasFile);
            _logger.LogInformation("TokensPerDocPath: {Path}", tokensPerDocPath);
            _logger.LogInformation("LemmasPerDocPath: {Path}", lemmasPerDocPath);
            _logger.LogInformation("OutputDir: {Path}", outputDir);

            _tfIdfService.Run(
                dataPath,
                lemmasFile,
                tokensPerDocPath,
                lemmasPerDocPath,
                outputDir);

            _logger.LogInformation("TF-IDF results saved to {Path}", outputDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TF-IDF calculation failed");
        }
        finally
        {
            _lifetime.StopApplication();
        }

        await Task.CompletedTask;
    }
}