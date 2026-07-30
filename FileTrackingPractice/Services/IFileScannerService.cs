using FileTrackingPractice.DTOs;

namespace FileTrackingPractice.Services
{
    public interface IFileScannerService
    {
        Task<ScanResultDto> ScanAsync(CancellationToken cancelToken = default);
    }
}
