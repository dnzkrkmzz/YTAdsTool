namespace YTReklamAraci.Models;

public class ChannelViewModel
{
    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public string BannerImageUrl { get; set; } = string.Empty;
    public ulong? SubscriberCount { get; set; }
    public ulong? VideoCount { get; set; }
    public ulong? ViewCount { get; set; }
    public string PublishedAt { get; set; } = string.Empty;
    public List<YoutubeVideo> TopVideos { get; set; } = new List<YoutubeVideo>();
    public string ErrorMessage { get; set; } = string.Empty;
    public string ChannelCategory { get; set; } = string.Empty;
}