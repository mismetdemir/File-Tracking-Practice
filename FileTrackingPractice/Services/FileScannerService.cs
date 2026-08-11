using FileTrackingPractice.Config;
using FileTrackingPractice.Data;
using FileTrackingPractice.DTOs;
using FileTrackingPractice.Exceptions;
using FileTrackingPractice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FileTrackingPractice.Services
{
    public class FileScannerService : IFileScannerService
    {
        private static readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);

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

        private static async Task<string> CalculateHashAsync(string filePath, CancellationToken cancelToken)
        {
            using FileStream stream = File.OpenRead(filePath);

            var hash = await SHA256.HashDataAsync(stream, cancelToken);

            return Convert.ToHexString(hash).ToLower();
        }

        public async Task<ScanResultDto> ScanAsync(CancellationToken cancelToken = default)
        {
            if (string.IsNullOrEmpty(_settings.FolderPath))
            {
                throw new FileScanConfigurationException("Scan folder is not configured");
            }

            if (!Directory.Exists(_settings.FolderPath))
            {
                throw new DirectoryNotFoundException($"Scan folder '{_settings.FolderPath}' does not exist");
            }

            await _scanLock.WaitAsync(cancelToken);


            try
            {
                var result = new ScanResultDto { ScanStartedAt = DateTime.Now };

                _logger.LogInformation("File scan started for folder {FolderPath}", _settings.FolderPath);

                var filePaths = Directory.EnumerateFiles(
                    _settings.FolderPath,
                    "*",
                    SearchOption.AllDirectories)
                .ToList();

                result.FilesFound = filePaths.Count;

                var existingPaths = await _context.FileRecords.Select(file => file.Path).ToHashSetAsync(cancelToken);

                foreach (var filePath in filePaths)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    try
                    {
                        var currentPath = Path.GetFullPath(filePath);

                        if (existingPaths.Contains(currentPath))
                        {
                            result.FilesSkipped++;
                            _logger.LogDebug("File {filePath} skipped because it already processed", filePath);
                            continue;
                        }

                        var fileInfo = new FileInfo(currentPath);
                        var hash = await CalculateHashAsync(currentPath, cancelToken);
                        var fileRecord = new FileRecord
                        {
                            Name = fileInfo.Name,
                            Extension = fileInfo.Extension.TrimStart('.').ToLower(),
                            Size = fileInfo.Length,
                            CreatedAt = fileInfo.CreationTime,
                            LastModifiedAt = fileInfo.LastWriteTime,
                            Path = currentPath,
                            Hash = hash
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
            finally 
            {
                _scanLock.Release();
            }
        }
    }
}