using FileTrackingPractice.Mappings;
using FileTrackingPractice.Config;
using FileTrackingPractice.Data;
using FileTrackingPractice.DTOs;
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
            var files = fileRecords.Select(file => FileRecordMapper.MapToDto(file, _settings.FolderPath)).ToList();

            return Ok(files);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FileRecordDto>> GetByIdAsync(int id, CancellationToken cancelToken)
        {
            var fileRecord = await _context.FileRecords.FirstOrDefaultAsync(file => file.Id == id, cancelToken);

            if (fileRecord == null)
            {
                return NotFound(new
                {
                    Message = $"File record with ID {id} was not found"
                });
            }

            var fileDto = FileRecordMapper.MapToDto(fileRecord, _settings.FolderPath);

            return Ok(fileDto);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<FileRecordDto>>> SearchByExtensionAsync(
            [FromQuery] string extension,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return BadRequest(new
                {
                    Message = "Extension is required"
                });
            }

            var normalizedExtension = extension.Trim().TrimStart('.').ToLower();
            var fileRecords = await _context.FileRecords
                .Where(file => file.Extension == normalizedExtension)
                .ToListAsync(cancelToken);
            var files = fileRecords.Select(file => FileRecordMapper.MapToDto(file, _settings.FolderPath)).ToList();

            return Ok(files);
        }
    }
}
