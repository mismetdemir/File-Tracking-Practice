using FileTrackingPractice.Config;
using FileTrackingPractice.Data;
using FileTrackingPractice.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingPractice.Controllers
{
    [Route("api/files")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FileScanSettings _settings;

        public FilesController(AppDbContext context, IOptions<FileScanSettings> settings)
        {
            _context = context;
            _settings = settings.Value;
        }

        [HttpGet]
        public async Task<ActionResult<List<FileRecordDto>>> GetAllAsync(CancellationToken cancelToken)
        {
            var fileRecords = await _context.FileRecords.ToListAsync(cancelToken);
            var rootPath = System.IO.Path.GetFullPath(_settings.FolderPath);
            var files = fileRecords.Select(file => new FileRecordDto
            {
                Id = file.Id,
                Name = file.Name,
                Extension = file.Extension,
                Size = file.Size,
                CreatedAt = file.CreatedAt,
                LastModifiedAt = file.LastModifiedAt,
                RelativePath = System.IO.Path.GetRelativePath(rootPath, file.Path)
            }).ToList();

            return Ok(files);
        }
    }
}
