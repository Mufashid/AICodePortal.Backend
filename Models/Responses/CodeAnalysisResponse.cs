public class CodeAnalysisResponse
{
    public string Response { get; set; } = string.Empty;
    public List<string> RelevantFiles { get; set; } = new List<string>();
    public string ProjectStructure { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }

    public string UsedProvider { get; set; } = string.Empty;
    public string UsedModel { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; } // Optional: show estimated cost
}