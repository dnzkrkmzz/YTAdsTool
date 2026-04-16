namespace YTReklamAraci.Models
{
    public class CommentHunterResult
    {
        public string? VideoId { get; set; }
        public string? Title { get; set; }
        public string? Thumbnail { get; set; }
        
        // Yeni Eklenenler
        public string? ChannelName { get; set; }
        public ulong? ViewCount { get; set; } 
        
        public int CommentCount { get; set; }
        public string? SampleComment { get; set; }
    }
}