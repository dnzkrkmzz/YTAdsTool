namespace YTReklamAraci.Models;

public class CommentItem
{
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorProfileImageUrl { get; set; } = string.Empty;
    public string TextOriginal { get; set; } = string.Empty;
    public long? LikeCount { get; set; }
    public string PublishedAt { get; set; } = string.Empty;
}

public class CommentAnalyzerViewModel
{
    public string VideoUrl { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public ulong? TotalCommentCount { get; set; }
    public List<CommentItem> Comments { get; set; } = new List<CommentItem>();
    public string ErrorMessage { get; set; } = string.Empty;
}