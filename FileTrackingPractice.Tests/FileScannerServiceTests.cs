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
    }
}
