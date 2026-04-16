namespace YTReklamAraci.Models;

public class TagAnalyzerViewModel
{
    public string VideoUrl { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string ErrorMessage { get; set; } = string.Empty;
}