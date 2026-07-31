using FileTrackingPractice.DTOs;
using FileTrackingPractice.Models;

namespace FileTrackingPractice.Mappings
{
    public class FileRecordMapper
    {
        public FileRecordDto MapToDto(FileRecord fileRecord, string rootFolderPath)
        {
            var rootPath = Path.GetFullPath(rootFolderPath);

            return new FileRecordDto
            {
                Id = fileRecord.Id,
                Name = fileRecord.Name,
                Extension = fileRecord.Extension,
                Size = fileRecord.Size,
                CreatedAt = fileRecord.CreatedAt,
                LastModifiedAt = fileRecord.LastModifiedAt,
                RelativePath = Path.GetRelativePath(rootPath, fileRecord.Path)
            };
        }
    }
}
