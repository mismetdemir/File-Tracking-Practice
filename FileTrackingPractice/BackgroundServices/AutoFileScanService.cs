using FileTrackingPractice.Config;
using FileTrackingPractice.Services;
using Microsoft.Extensions.Options;

namespace FileTrackingPractice.BackgroundServices
{
    public class AutoFileScanService : BackgroundService
    {
        private readonly FileScanSettings _settings;
        private readonly ILogger<AutoFileScanService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public AutoFileScanService(
            IOptions<FileScanSettings> settings,
            ILogger<AutoFileScanService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _settings = settings.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancelToken)
        {
            if (_settings.IntervalInSeconds <= 0)
            {
                _logger.LogError("File scan interval must be greater than zero");
                return;
            }
                
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var fileScannerService = scope.ServiceProvider.GetRequiredService<IFileScannerService>();

                    await fileScannerService.ScanAsync(cancelToken);
                }
                catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured during the automatic file scan");
                }


                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalInSeconds), cancelToken);
                }
                catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

    }
}
