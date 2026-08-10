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
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

            return new AppDbContext(options);
        }

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
    }
}
