using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using FileTrackingPractice.Data;
using FileTrackingPractice.Config;
using FileTrackingPractice.Services;
using FileTrackingPractice.Exceptions;
using Moq;
using Castle.Core.Logging;
using Xunit;

namespace FileTrackingPractice.Tests
{
    public class FileScannerServiceTests
    {
        private static AppDbContext GetDbContext()
        {
            return GetDbContext(Guid.NewGuid().ToString());
        }

        private static AppDbContext GetDbContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            return new AppDbContext(options);
        }

        // ########## Exception Tests ##########

        [Fact]
        public async Task ScanAsync_WhenFolderPathIsEmpty_ThrowsFileScanConfigurationException()
        {
            using var context = GetDbContext();

            var settingsMock = new Mock<IOptions<FileScanSettings>>();
            var loggerMock = new Mock<ILogger<FileScannerService>>();
            
            settingsMock
                .Setup(settings => settings.Value)
                .Returns(new FileScanSettings
                {
                    FolderPath = string.Empty
                });

            var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

            await Assert.ThrowsAsync<FileScanConfigurationException>(() => service.ScanAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ScanAsync_WhenFolderDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            using var context = GetDbContext();

            var settingsMock = new Mock<IOptions<FileScanSettings>>();
            var loggerMock = new Mock<ILogger<FileScannerService>>();

            settingsMock
                .Setup(settings => settings.Value)
                .Returns(new FileScanSettings
                {
                    FolderPath = "NonExistentFolder"
                });

            var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.ScanAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ScanAsync_WhenCancellationRequested_ShouldThrowOperationCancelledException()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                using var cancellationTokenSource = new CancellationTokenSource();

                cancellationTokenSource.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => service.ScanAsync(cancellationTokenSource.Token));
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        // ########## ScanLock Tests ##########

        [Fact]
        public async Task ScanAsync_WhenCalledSimultaneously_ShouldAddFileOnlyOnce()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.txt");
                await File.WriteAllTextAsync(filePath, "Test text", TestContext.Current.CancellationToken);

                var databaseName = Guid.NewGuid().ToString();

                using var context1 = GetDbContext(databaseName);
                using var context2 = GetDbContext(databaseName);

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock1 = new Mock<ILogger<FileScannerService>>();
                var loggerMock2 = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service1 = new FileScannerService(context1, settingsMock.Object, loggerMock1.Object);
                var service2 = new FileScannerService(context2, settingsMock.Object, loggerMock2.Object);

                var scan1 = service1.ScanAsync(TestContext.Current.CancellationToken);
                var scan2 = service2.ScanAsync(TestContext.Current.CancellationToken);

                var results = await Task.WhenAll(scan1, scan2);

                Assert.Equal(1, results.Sum(result => result.FilesAdded));
                Assert.Equal(1, results.Sum(result => result.FilesSkipped));

                using var verificationContext = GetDbContext(databaseName);
                var records = await verificationContext.FileRecords.ToListAsync(TestContext.Current.CancellationToken);

                Assert.Single(records);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        // ########## Scan Tests ##########

        [Fact]
        public async Task ScanAsync_WhenFolderIsEmpty_ShouldReturnZeroCounts()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                var result = await service.ScanAsync(TestContext.Current.CancellationToken);

                Assert.Equal(0, result.FilesFound);
                Assert.Equal(0, result.FilesAdded);
                Assert.Equal(0, result.FilesUpdated);
                Assert.Equal(0, result.FilesSkipped);
                Assert.Equal(0, result.FilesFailed);
                Assert.Empty(context.FileRecords);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileExists_ShouldSaveFileMetadata()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.pdf");
                await File.WriteAllTextAsync(filePath, "test text", TestContext.Current.CancellationToken);

                var fileInfo = new FileInfo(filePath);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);

                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.Equal(fileInfo.Name, record.Name);
                Assert.Equal("pdf", record.Extension);
                Assert.Equal(fileInfo.Length, record.Size);
                Assert.Equal(fileInfo.CreationTime, record.CreatedAt);
                Assert.Equal(fileInfo.LastWriteTime, record.LastModifiedAt);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenMultipleNewFilesExist_ShouldAddAllFiles()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath1 = Path.Combine(folderPath, "file1.txt");
                var filePath2 = Path.Combine(folderPath, "file2.pdf");

                await File.WriteAllTextAsync(filePath1, "file 1 test text", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(filePath2, "file 2 test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                var result = await service.ScanAsync(TestContext.Current.CancellationToken);
                var records = await context.FileRecords.ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, result.FilesFound);
                Assert.Equal(2, result.FilesAdded);
                Assert.Equal(0, result.FilesUpdated);
                Assert.Equal(0, result.FilesSkipped);
                Assert.Equal(0, result.FilesFailed);
                Assert.Equal(2, records.Count);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileAlreadyExists_ShouldSkipFile()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.txt");
                await File.WriteAllTextAsync(filePath, "Test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);
                var secondResult = await service.ScanAsync(TestContext.Current.CancellationToken);

                Assert.Equal(1, secondResult.FilesFound);
                Assert.Equal(0, secondResult.FilesAdded);
                Assert.Equal(0, secondResult.FilesUpdated);
                Assert.Equal(1, secondResult.FilesSkipped);
                Assert.Equal(0, secondResult.FilesFailed);
                Assert.Single(context.FileRecords);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileExistsInSubfolder_ShouldAddFile()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var subfolderPath = Path.Combine(folderPath, "Subfolder");

            Directory.CreateDirectory(subfolderPath);

            try
            {
                var filePath = Path.Combine(subfolderPath, "test.txt");
                await File.WriteAllTextAsync(filePath, "Test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                var result = await service.ScanAsync(TestContext.Current.CancellationToken);
                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.Equal(1, result.FilesFound);
                Assert.Equal(1, result.FilesAdded);
                Assert.Equal("test.txt", record.Name);
                Assert.Equal(Path.GetFullPath(filePath), record.Path);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileExtensionIsUppercase_ShouldSaveExtensionAsLowercase()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.PDF");
                await File.WriteAllTextAsync(filePath, "test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);
                
                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.Equal("pdf", record.Extension);

            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileHasNoExtension_ShouldSaveExtensionAsEmptyString()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test");
                await File.WriteAllTextAsync(filePath, "test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);

                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.Equal("test", record.Name);
                Assert.Empty(record.Extension);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenSameNamedFilesExistInDifferentSubfolders_ShouldAddDistinctRecords()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var subfolderPath1 = Path.Combine(folderPath, "Subfolder1");
            var subfolderPath2 = Path.Combine(folderPath, "Subfolder2");
            var subfolderPath3 = Path.Combine(subfolderPath2, "Sub-Subfolder");

            Directory.CreateDirectory(subfolderPath1);
            Directory.CreateDirectory(subfolderPath3);

            try
            {
                var filePath1 = Path.Combine(folderPath, "test.txt");
                var filePath2 = Path.Combine(subfolderPath1, "test.txt");
                var filePath3 = Path.Combine(subfolderPath2, "test.txt");
                var filePath4 = Path.Combine(subfolderPath3, "test.txt");

                await File.WriteAllTextAsync(filePath1, "top folder file", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(filePath2, "first subfolder file", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(filePath3, "second subfolder file", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(filePath4, "sub-subfolder file", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                var result = await service.ScanAsync(TestContext.Current.CancellationToken);
                var records = await context.FileRecords.ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(4, result.FilesFound);
                Assert.Equal(4, result.FilesAdded);
                Assert.Equal(0, result.FilesUpdated);
                Assert.Equal(0, result.FilesSkipped);
                Assert.Equal(0, result.FilesFailed);
                
                Assert.Equal(4, records.Count);
                Assert.All(records, record => Assert.Equal("test.txt", record.Name));
                Assert.Equal(4, records.Select(record => record.Path).Distinct().Count());
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenExistingAndNewFilesExist_ShouldAddNewAndSkipExisting()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath1 = Path.Combine(folderPath, "test1.txt");
                var filePath2 = Path.Combine(folderPath, "test2.txt");

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await File.WriteAllTextAsync(filePath1, "test text 1", TestContext.Current.CancellationToken);
                await service.ScanAsync(TestContext.Current.CancellationToken);

                await File.WriteAllTextAsync(filePath2, "test text 2", TestContext.Current.CancellationToken);
                var result = await service.ScanAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, result.FilesFound);
                Assert.Equal(1, result.FilesAdded);
                Assert.Equal(0, result.FilesUpdated);
                Assert.Equal(1, result.FilesSkipped);
                Assert.Equal(0, result.FilesFailed);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileExists_ShouldSaveSHA256Hash()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.txt");
                await File.WriteAllTextAsync(filePath, "Test text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);

                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.False(string.IsNullOrEmpty(record.Hash));
                Assert.Equal(64, record.Hash.Length);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenFileContentChanges_ShouldUpdateExistingFile()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                var filePath = Path.Combine(folderPath, "test.txt");
                await File.WriteAllTextAsync(filePath, "Original text", TestContext.Current.CancellationToken);

                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);

                var record = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);
                var oldHash = record.Hash;

                await File.WriteAllTextAsync(filePath, "Changed text", TestContext.Current.CancellationToken);
                
                var result = await service.ScanAsync(TestContext.Current.CancellationToken);
                var updatedRecord = await context.FileRecords.SingleAsync(TestContext.Current.CancellationToken);

                Assert.Equal(1, result.FilesFound);
                Assert.Equal(0, result.FilesAdded);
                Assert.Equal(1, result.FilesUpdated);
                Assert.Equal(0, result.FilesSkipped);
                Assert.Equal(0, result.FilesFailed);

                Assert.NotEqual(oldHash, updatedRecord.Hash);
                Assert.Single(context.FileRecords);

            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenScanCompletes_ShouldSetScanTimestamps()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                var result = await service.ScanAsync(TestContext.Current.CancellationToken);

                Assert.NotEqual(default, result.ScanStartedAt);
                Assert.NotEqual(default, result.ScanFinishedAt);
                Assert.True(result.ScanFinishedAt >= result.ScanStartedAt);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }

        [Fact]
        public async Task ScanAsync_WhenScanCompletes_ShouldWriteInformationLog()
        {
            var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(folderPath);

            try
            {
                using var context = GetDbContext();

                var settingsMock = new Mock<IOptions<FileScanSettings>>();
                var loggerMock = new Mock<ILogger<FileScannerService>>();

                settingsMock
                    .Setup(settings => settings.Value)
                    .Returns(new FileScanSettings
                    {
                        FolderPath = folderPath
                    });

                var service = new FileScannerService(context, settingsMock.Object, loggerMock.Object);

                await service.ScanAsync(TestContext.Current.CancellationToken);

                loggerMock.Verify(
                    logger => logger.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((value, type) => value.ToString()!.Contains("File scan completed.")),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
            }
            finally
            {
                Directory.Delete(folderPath, true);
            }
        }
    }
}
