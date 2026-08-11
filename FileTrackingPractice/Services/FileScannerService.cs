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

                var existingFiles = await _context.FileRecords
                    .ToDictionaryAsync(
                        file => file.Path,
                        file => file,
                        cancelToken);

                foreach (var filePath in filePaths)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    try
                    {
                        var currentPath = Path.GetFullPath(filePath);
                        var hash = await CalculateHashAsync(currentPath, cancelToken);
                        var fileInfo = new FileInfo(currentPath);

                        if (existingFiles.TryGetValue(currentPath, out var existingFile))
                        {
                            if (existingFile.Hash == hash)
                            {
                                result.FilesSkipped++;
                                _logger.LogDebug("File {filePath} skipped because file content did not change", filePath);

                                continue;
                            }

                            existingFile.Name = fileInfo.Name;
                            existingFile.Extension = fileInfo.Extension.TrimStart('.').ToLower();
                            existingFile.Size = fileInfo.Length;
                            existingFile.CreatedAt = fileInfo.CreationTime;
                            existingFile.LastModifiedAt = fileInfo.LastWriteTime;
                            existingFile.Hash = hash;

                            result.FilesUpdated++;
                            _logger.LogInformation("File {filePath} changes was staged for database update", filePath);

                            continue;
                        }
                        
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

                if (result.FilesAdded > 0 || result.FilesUpdated > 0 )
                {
                    await _context.SaveChangesAsync(cancelToken);
                }

                result.ScanFinishedAt = DateTime.Now;
                _logger.LogInformation("File scan completed. " +
                    "Found: {FilesFound}, " +
                    "Added: {FilesAdded}, " +
                    "Updated: {FilesUpdated}, " +
                    "Skipped: {FilesSkipped}, " +
                    "Failed: {FilesFailed}",
                    result.FilesFound, result.FilesAdded, result.FilesUpdated, result.FilesSkipped, result.FilesFailed);

                return result;
            }
            finally 
            {
                _scanLock.Release();
            }
        }
    }
}