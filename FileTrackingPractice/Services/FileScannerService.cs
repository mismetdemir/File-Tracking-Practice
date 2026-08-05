using FileTrackingPractice.Config;
using FileTrackingPractice.Data;
using FileTrackingPractice.DTOs;
using FileTrackingPractice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileTrackingPractice.Services
{
    public class FileScannerService : IFileScannerService
    {
        private readonly AppDbContext _context;
        private readonly FileScanSettings _settings;
        private readonly ILogger<FileScannerService> _logger;

        public FileScannerService(
            AppDbContext context,
            IOptions<FileScanSettings> settings,
            ILogger<FileScannerService> logger)
        {
            _context = context;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ScanResultDto> ScanAsync(CancellationToken cancelToken = default)
        {
            var result = new ScanResultDto { ScanStartedAt = DateTime.Now };

            if (string.IsNullOrEmpty(_settings.FolderPath))
            {
                throw new InvalidOperationException("Scan folder is not configured");
            }

            if (!Directory.Exists(_settings.FolderPath))
            {
                throw new DirectoryNotFoundException($"Scan folder '{_settings.FolderPath}' does not exist");
            }

            _logger.LogInformation("File scan started for folder {FolderPath}", _settings.FolderPath);


            var filePaths = Directory.EnumerateFiles(
                _settings.FolderPath,
                "*", 
                SearchOption.AllDirectories)
            .ToList();

            result.FilesFound = filePaths.Count;


            foreach (var filePath in filePaths)
            {
                cancelToken.ThrowIfCancellationRequested();

                try
                {
                    var currentPath = Path.GetFullPath(filePath);

                    var alreadyExists = await _context.FileRecords.AnyAsync(
                        file => file.Path == currentPath,
                        cancelToken);

                    if (alreadyExists)
                    {
                        result.FilesSkipped++;
                        _logger.LogInformation("File {filePath} skipped because it already processed", filePath);
                        continue;
                    }


                    var fileInfo = new FileInfo(currentPath);
                    var fileRecord = new FileRecord
                    {
                        Name = fileInfo.Name,
                        Extension = fileInfo.Extension.TrimStart('.').ToLower(),
                        Size = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTime,
                        LastModifiedAt = fileInfo.LastWriteTime,
                        Path = currentPath
                    };

                    await _context.FileRecords.AddAsync(fileRecord, cancelToken);
                    result.FilesAdded++;
                    _logger.LogInformation("File {filePath} was staged for database insertion", filePath);
                } 
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.FilesFailed++;
                    _logger.LogError(ex, "An error occured while processing file {filePath}", filePath);
                }
            }

            if (result.FilesAdded > 0)
            {
                await _context.SaveChangesAsync(cancelToken);
            }

            result.ScanFinishedAt = DateTime.Now;
            _logger.LogInformation("File scan completed. " +
                "Found: {FilesFound}, Added: {FilesAdded}, Skipped: {FilesSkipped}, Failed: {FilesFailed}",
                result.FilesFound, result.FilesAdded, result.FilesSkipped, result.FilesFailed);

            return result;
        }
    }
}