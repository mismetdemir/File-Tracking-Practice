namespace FileTrackingPractice.DTOs
{
    public class ScanResultDto
    {
        public int FilesFound { get; set; }
        public int FilesAdded { get; set; }
        public int FilesSkipped { get; set; }
        public int FilesFailed { get; set; }
        public DateTime ScanStartedAt { get; set; }
        public DateTime ScanFinishedAt { get; set; }
    }
}
