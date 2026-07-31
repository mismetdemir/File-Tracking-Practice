namespace FileTrackingPractice.DTOs
{
    public class FileRecordDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
        public string RelativePath { get; set; } = string.Empty;
    }
}
