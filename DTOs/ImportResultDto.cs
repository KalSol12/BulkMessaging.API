namespace BulkMessaging.API.DTOs;

public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public List<ImportErrorDto> Errors { get; set; } = new();
}

public class ImportErrorDto
{
    public int Line { get; set; }
    public string Message { get; set; } = string.Empty;
}