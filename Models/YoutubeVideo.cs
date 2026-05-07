namespace YTReklamAraci.Models;

public class YoutubeVideo
{
    public string Title { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public ulong? ViewCount { get; set; }
    public ulong? LikeCount { get; set; }
    public string PublishedAt { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}