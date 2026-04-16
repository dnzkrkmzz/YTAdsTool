using System.ComponentModel.DataAnnotations;

namespace YTReklamAraci.Models;

public class SearchCache
{
    [Key]
    public int Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string VideoDataJson { get; set; } = string.Empty; // Videoları JSON olarak saklayacağız
    public DateTime SearchDate { get; set; } = DateTime.Now;
    public int SearchCount { get; set; } = 1;
}