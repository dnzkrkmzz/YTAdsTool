using Microsoft.AspNetCore.Mvc;
using YTReklamAraci.Models;
using YTReklamAraci.Data;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Newtonsoft.Json;
using ClosedXML.Excel;
using System.IO;
using System.Text.RegularExpressions;

namespace YTReklamAraci.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;

    public HomeController(IConfiguration config, ApplicationDbContext context)
    {
        _config = config;
        _context = context;
    }

    // YouTube Kategori Numaralarını Türkçeye Çeviren Sözlük (YENİ EKLENDİ)
    private string GetCategoryName(string categoryId)
    {
        return categoryId switch
        {
            "1" => "🎬 Film/Animasyon",
            "2" => "🚗 Otomobiller",
            "10" => "🎵 Müzik",
            "15" => "🐾 Evcil Hayvanlar",
            "17" => "⚽ Spor",
            "20" => "🎮 Oyun",
            "22" => "👤 Kişiler/Bloglar",
            "23" => "🤣 Komedi",
            "24" => "🎭 Eğlence",
            "25" => "📰 Haber/Politika",
            "26" => "👗 Stil/Nasıl Yapılır",
            "27" => "📚 Eğitim",
            "28" => "🔬 Bilim/Teknoloji",
            _ => "Bilinmeyen (" + categoryId + ")"
        };
    }

    public IActionResult Index()
    {
        var today = DateTime.Today; 

        ViewBag.TopTrends = _context.SearchCaches
            .Where(x => x.SearchDate >= today) // Sadece bugün arananları getir
            .OrderByDescending(x => x.SearchCount)
            .Take(5)
            .Select(x => x.Keyword)
            .ToList();

        return View(new List<YoutubeVideo>());
    }

    [HttpPost]
    [Route("search")]
    public async Task<IActionResult> Search(string keyword, List<string> excludedCategories, string dateFilter = "all", string durationFilter = "all", bool hdOnly = false, string sortBy = "relevance")
    {
        if (string.IsNullOrEmpty(keyword)) return View("Index", new List<YoutubeVideo>());

        var keywordList = keyword.Split(',')
                                .Select(k => k.ToLower().Trim())
                                .Where(k => !string.IsNullOrEmpty(k))
                                .Distinct()
                                .ToList();

        var allVideos = new List<YoutubeVideo>();
        var apiKey = _config["YouTubeSettings:ApiKey"];
        var youtubeService = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });

        foreach (var word in keywordList)
        {
            // YENİ: Cache ismini ayarlarken hariç tutulan kategorileri de dahil ediyoruz
            string excludeKey = excludedCategories != null && excludedCategories.Any() ? string.Join("-", excludedCategories) : "none";
            bool hasFilter = dateFilter != "all" || durationFilter != "all" || hdOnly || (excludedCategories != null && excludedCategories.Any()) || sortBy != "relevance";
            string cacheKey = hasFilter ? $"{word}|{dateFilter}|{durationFilter}|{hdOnly}|{excludeKey}|{sortBy}" : word;

            var cachedResult = _context.SearchCaches.FirstOrDefault(s => s.Keyword == cacheKey);

            if (cachedResult != null)
            {
                cachedResult.SearchCount++; 
                if (cachedResult.SearchDate > DateTime.Now.AddDays(-1))
                {
                    await _context.SaveChangesAsync();
                    var cachedVideos = JsonConvert.DeserializeObject<List<YoutubeVideo>>(cachedResult.VideoDataJson);
                    if (cachedVideos != null) allVideos.AddRange(cachedVideos);
                    continue; 
                }
            }

            var searchRequest = youtubeService.Search.List("snippet");
            searchRequest.Q = word;
            searchRequest.Type = "video";
            searchRequest.MaxResults = 50; // API kotasını aşmadan maksimum havuz

            // YENİ: SIRALAMA MANTIĞI
            searchRequest.Order = sortBy switch
            {
                "date" => SearchResource.ListRequest.OrderEnum.Date, // En Yeni
                "viewCount" => SearchResource.ListRequest.OrderEnum.ViewCount, // En Çok İzlenen
                _ => SearchResource.ListRequest.OrderEnum.Relevance // Alaka Düzeyi (Varsayılan)
            };

            // YENİ EKLENEN FİLTRE MANTIKLARI
            if (dateFilter != "all")
            {
                searchRequest.PublishedAfterDateTimeOffset = dateFilter switch
                {
                    "today" => DateTimeOffset.UtcNow.AddDays(-1),
                    "week" => DateTimeOffset.UtcNow.AddDays(-7),
                    "month" => DateTimeOffset.UtcNow.AddMonths(-1),
                    "year" => DateTimeOffset.UtcNow.AddYears(-1),
                    _ => null
                };
            }

            if (durationFilter != "all")
            {
                searchRequest.VideoDuration = durationFilter switch
                {
                    "short" => SearchResource.ListRequest.VideoDurationEnum.Short__,
                    "medium" => SearchResource.ListRequest.VideoDurationEnum.Medium,
                    "long" => SearchResource.ListRequest.VideoDurationEnum.Long__,
                    _ => SearchResource.ListRequest.VideoDurationEnum.Any
                };
            }

            if (hdOnly) searchRequest.VideoDefinition = SearchResource.ListRequest.VideoDefinitionEnum.High;

            var searchResponse = await searchRequest.ExecuteAsync();
            var videoIds = searchResponse.Items.Select(i => i.Id.VideoId).ToList();

            if (!videoIds.Any()) continue;

            var videoRequest = youtubeService.Videos.List("snippet,statistics");
            videoRequest.Id = string.Join(",", videoIds);
            var videoResponse = await videoRequest.ExecuteAsync();

            // İŞTE SİHİRLİ KISIM: HARİÇ TUTULAN KATEGORİLERİ ELEME
            var filteredItems = videoResponse.Items.AsEnumerable();

            if (excludedCategories != null && excludedCategories.Any())
            {
                // Videonun kategori ID'si, kullanıcının elediği listede YOKSA onu alıyoruz.
                filteredItems = filteredItems.Where(item => !excludedCategories.Contains(item.Snippet.CategoryId));
            }

            // Eleme yapıldıktan sonra kalanlardan en tepeki 20 tanesini alıp dönüştürüyoruz
            var videoList = filteredItems.Take(20).Select(item => new YoutubeVideo
            {
                Title = item.Snippet.Title,
                VideoUrl = "https://www.youtube.com/watch?v=" + item.Id,
                ChannelTitle = item.Snippet.ChannelTitle,
                ChannelUrl = "https://www.youtube.com/channel/" + item.Snippet.ChannelId,
                ViewCount = item.Statistics.ViewCount,
                LikeCount = item.Statistics.LikeCount,
                PublishedAt = item.Snippet.PublishedAtDateTimeOffset?.ToString("dd.MM.yyyy") ?? "",
                CategoryName = GetCategoryName(item.Snippet.CategoryId) // YENİ: Kategori ismini modele aktarıyoruz
            }).ToList();

            allVideos.AddRange(videoList);

            if (cachedResult != null)
            {
                cachedResult.VideoDataJson = JsonConvert.SerializeObject(videoList);
                cachedResult.SearchDate = DateTime.Now;
                _context.SearchCaches.Update(cachedResult);
            }
            else
            {
                var newCache = new SearchCache
                {
                    Keyword = cacheKey,
                    VideoDataJson = JsonConvert.SerializeObject(videoList),
                    SearchDate = DateTime.Now,
                    SearchCount = 1
                };
                _context.SearchCaches.Add(newCache);
            }
            await _context.SaveChangesAsync();
        }

        // Çoklu kelime arandığında bile ekrana basılacak maksimum video sayısını 50 ile sınırladık (Take 50)
        var distinctVideos = allVideos.GroupBy(v => v.VideoUrl).Select(g => g.First()).Take(50).ToList();

        // Trendlerde sadece "filtresiz" aramaları VE SADECE BUGÜN arananları gösteriyoruz
        var today = DateTime.Today; 
        
        ViewBag.TopTrends = _context.SearchCaches
            .Where(x => !x.Keyword.Contains("|") && x.SearchDate >= today)
            .OrderByDescending(x => x.SearchCount)
            .Take(5)
            .Select(x => x.Keyword)
            .ToList();

        return View("Index", distinctVideos);
    }

    [HttpPost]
    public IActionResult ExportToExcel(List<YoutubeVideo> videos)
    {
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Hedefleme Listesi");
            worksheet.Cell(1, 1).Value = "Video Başlığı";
            worksheet.Cell(1, 2).Value = "Video URL";
            worksheet.Cell(1, 3).Value = "Kanal Adı";
            worksheet.Cell(1, 4).Value = "İzlenme";
            worksheet.Cell(1, 5).Value = "Yayın Tarihi";

            for (int i = 0; i < videos.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = videos[i].Title;
                worksheet.Cell(i + 2, 2).Value = videos[i].VideoUrl;
                worksheet.Cell(i + 2, 3).Value = videos[i].ChannelTitle;
                worksheet.Cell(i + 2, 4).Value = videos[i].ViewCount?.ToString() ?? "0";
                worksheet.Cell(i + 2, 5).Value = videos[i].PublishedAt;
            }

            worksheet.Columns().AdjustToContents(); 

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "YTtarget.xlsx");
            }
        }
    }

    // --- YENİ ARAÇ: SEO VE ETİKET ANALİZİ ---
    [Route("taganalyzer")]
    public IActionResult TagAnalyzer()
    {
        return View(new TagAnalyzerViewModel());
    }

    [HttpPost]
    [Route("taganalyzer")]
    public async Task<IActionResult> TagAnalyzer(string videoUrl)
    {
        var model = new TagAnalyzerViewModel { VideoUrl = videoUrl };

        if (string.IsNullOrEmpty(videoUrl))
        {
            model.ErrorMessage = "Lütfen geçerli bir YouTube URL'si girin.";
            return View(model);
        }

        // URL'den 11 haneli Video ID'yi çıkaran Regex formülü
        var match = Regex.Match(videoUrl, @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})");
        
        if (!match.Success)
        {
            model.ErrorMessage = "Video linki anlaşılamadı. Lütfen tam linki kopyaladığınızdan emin olun.";
            return View(model);
        }

        var videoId = match.Groups[1].Value;

        // API'ye bağlan ve video detaylarını çek
        var apiKey = _config["YouTubeSettings:ApiKey"];
        var youtubeService = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });

        var videoRequest = youtubeService.Videos.List("snippet");
        videoRequest.Id = videoId;
        var videoResponse = await videoRequest.ExecuteAsync();

        var videoItem = videoResponse.Items.FirstOrDefault();

        if (videoItem == null)
        {
            model.ErrorMessage = "Video bulunamadı. Gizli veya silinmiş olabilir.";
            return View(model);
        }

        // Gelen verileri modele aktar
        model.VideoTitle = videoItem.Snippet.Title;
        model.ChannelName = videoItem.Snippet.ChannelTitle;
        model.ThumbnailUrl = videoItem.Snippet.Thumbnails.High?.Url ?? videoItem.Snippet.Thumbnails.Default__.Url;
        
        // Videonun etiketleri varsa listeye at, yoksa boş liste döndür
        if (videoItem.Snippet.Tags != null)
        {
            model.Tags = videoItem.Snippet.Tags.ToList();
        }

        return View(model);
    }

    // --- YENİ ARAÇ: DETAYLI KANAL ANALİZİ ---
    [Route("channelanalyzer")]
    public IActionResult ChannelAnalyzer()
    {
        return View(new ChannelViewModel());
    }

    [HttpPost]
    [Route("channelanalyzer")]
    public async Task<IActionResult> ChannelAnalyzer(string channelUrl)
    {
        var model = new ChannelViewModel();

        if (string.IsNullOrEmpty(channelUrl))
        {
            model.ErrorMessage = "Lütfen bir kanal linki veya ID'si girin.";
            return View(model);
        }

        // Kanal ID'sini linkten ayıklama (Basit yöntem)
        string channelId = "";
        if (channelUrl.Contains("channel/")) 
            channelId = channelUrl.Split("channel/")[1].Split('/')[0].Split('?')[0];
        else if (channelUrl.Contains("@")) // Handle handles
             channelId = channelUrl; // Handle'lar için özel arama gerekecek, şimdilik direkt ID kabul edelim
        else 
            channelId = channelUrl;

        var apiKey = _config["YouTubeSettings:ApiKey"];
        var youtubeService = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });

        // 1. Kanal Bilgilerini Çek
        var channelRequest = youtubeService.Channels.List("snippet,statistics,brandingSettings");
        
        if (channelId.StartsWith("@")) channelRequest.ForHandle = channelId;
        else channelRequest.Id = channelId;

        var channelResponse = await channelRequest.ExecuteAsync();
        var channelItem = channelResponse.Items?.FirstOrDefault();

        if (channelItem == null)
        {
            model.ErrorMessage = "Kanal bulunamadı. Lütfen ID'yi kontrol edin.";
            return View(model);
        }

        model.ChannelId = channelItem.Id;
        model.Title = channelItem.Snippet.Title;
        model.Description = channelItem.Snippet.Description;
        model.ProfileImageUrl = channelItem.Snippet.Thumbnails.High?.Url ?? "";
        model.BannerImageUrl = channelItem.BrandingSettings.Image?.BannerExternalUrl ?? "";
        model.SubscriberCount = channelItem.Statistics.SubscriberCount;
        model.VideoCount = channelItem.Statistics.VideoCount;
        model.ViewCount = channelItem.Statistics.ViewCount;
        model.PublishedAt = channelItem.Snippet.PublishedAtDateTimeOffset?.ToString("dd.MM.yyyy") ?? "";

        // 2. Kanalın En Popüler 5 Videosunu Çek
        var searchRequest = youtubeService.Search.List("snippet");
        searchRequest.ChannelId = channelItem.Id;
        searchRequest.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
        searchRequest.Type = "video";
        searchRequest.MaxResults = 5;
        var searchResponse = await searchRequest.ExecuteAsync();

        model.TopVideos = searchResponse.Items.Select(v => new YoutubeVideo {
            Title = v.Snippet.Title,
            VideoUrl = "https://www.youtube.com/watch?v=" + v.Id.VideoId,
            PublishedAt = v.Snippet.PublishedAtDateTimeOffset?.ToString("dd.MM.yyyy") ?? ""
        }).ToList();

        return View(model);
    }

    // --- YENİ ARAÇ: YORUM VE KİTLE ANALİZİ ---
    [Route("commentanalyzer")]
    public IActionResult CommentAnalyzer()
    {
        return View(new CommentAnalyzerViewModel());
    }

    [HttpPost]
    [Route("commentanalyzer")]
    public async Task<IActionResult> CommentAnalyzer(string videoUrl)
    {
        var model = new CommentAnalyzerViewModel { VideoUrl = videoUrl };

        if (string.IsNullOrEmpty(videoUrl))
        {
            model.ErrorMessage = "Lütfen geçerli bir YouTube URL'si girin.";
            return View(model);
        }

        var match = Regex.Match(videoUrl, @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})");
        if (!match.Success)
        {
            model.ErrorMessage = "Video linki anlaşılamadı. Lütfen tam linki kopyaladığınızdan emin olun.";
            return View(model);
        }

        var videoId = match.Groups[1].Value;
        var apiKey = _config["YouTubeSettings:ApiKey"];
        var youtubeService = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });

        try
        {
            // 1. Önce Video Detaylarını Çek
            var videoRequest = youtubeService.Videos.List("snippet,statistics");
            videoRequest.Id = videoId;
            var videoResponse = await videoRequest.ExecuteAsync();
            var videoItem = videoResponse.Items?.FirstOrDefault();

            if (videoItem == null)
            {
                model.ErrorMessage = "Video bulunamadı. Gizli veya silinmiş olabilir.";
                return View(model);
            }

            model.VideoTitle = videoItem.Snippet.Title;
            model.ChannelName = videoItem.Snippet.ChannelTitle;
            model.ThumbnailUrl = videoItem.Snippet.Thumbnails.High?.Url ?? "";
            model.TotalCommentCount = videoItem.Statistics.CommentCount;

            // 2. Videonun En Alakalı 50 Yorumunu Çek
            var commentRequest = youtubeService.CommentThreads.List("snippet");
            commentRequest.VideoId = videoId;
            commentRequest.MaxResults = 50; 
            commentRequest.Order = CommentThreadsResource.ListRequest.OrderEnum.Relevance; // En çok beğeni alanları/alakalıları getir
            
            var commentResponse = await commentRequest.ExecuteAsync();

            model.Comments = commentResponse.Items.Select(c => new CommentItem
            {
                AuthorName = c.Snippet.TopLevelComment.Snippet.AuthorDisplayName,
                AuthorProfileImageUrl = c.Snippet.TopLevelComment.Snippet.AuthorProfileImageUrl,
                TextOriginal = c.Snippet.TopLevelComment.Snippet.TextOriginal, // Yorumun saf metni
                LikeCount = c.Snippet.TopLevelComment.Snippet.LikeCount,
                PublishedAt = c.Snippet.TopLevelComment.Snippet.PublishedAtDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? ""
            }).ToList();
        }
        catch (Google.GoogleApiException ex) when (ex.Message.Contains("disabled comments"))
        {
            model.ErrorMessage = "Bu videonun yorumları kanal sahibi tarafından kapatılmış.";
        }
        catch (Exception)
        {
            model.ErrorMessage = "Yorumlar çekilirken beklenmeyen bir hata oluştu.";
        }

        return View(model);
    }

    [HttpGet]
    [Route("commenthunter")]
    // GET: Yorum Avcısı Sayfası
    public IActionResult CommentHunter()
    {
        return View();
    }

// POST: Yorumlarda Arama Yapma
    [HttpPost]
    [Route("commenthunter")]
    public async Task<IActionResult> CommentHunter(string mainKeywords, string commentKeywords, string dateFilter, string durationFilter, bool hdOnly)
    {
        if (string.IsNullOrEmpty(mainKeywords) || string.IsNullOrEmpty(commentKeywords))
        {
            ViewBag.Error = "Lütfen her iki alanı da doldurun.";
            return View();
        }

        var mainKeywordList = mainKeywords.Split(',').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).Distinct().Take(10).ToList();
        string youtubeSearchQuery = string.Join(" | ", mainKeywordList);

        var commentKeywordList = commentKeywords.Split(',').Select(k => k.Trim().ToLower()).Where(k => !string.IsNullOrEmpty(k)).Distinct().Take(10).ToList();

        if (!mainKeywordList.Any() || !commentKeywordList.Any())
        {
            ViewBag.Error = "Lütfen geçerli kelimeler girin.";
            return View();
        }

        var youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = _config["YouTubeSettings:ApiKey"], 
            ApplicationName = "YTReklamAraci"
        });

        var searchRequest = youtubeService.Search.List("snippet");
        searchRequest.Q = youtubeSearchQuery;
        searchRequest.MaxResults = 25; 
        searchRequest.Type = "video";

        // --- EKLENEN GELİŞMİŞ FİLTRELER BURADA BAŞLIYOR ---
        if (hdOnly)
        {
            searchRequest.VideoDefinition = SearchResource.ListRequest.VideoDefinitionEnum.High;
        }

        if (!string.IsNullOrEmpty(durationFilter) && durationFilter != "any")
        {
            searchRequest.VideoDuration = durationFilter switch
            {
                "short" => SearchResource.ListRequest.VideoDurationEnum.Short__,
                "medium" => SearchResource.ListRequest.VideoDurationEnum.Medium,
                "long" => SearchResource.ListRequest.VideoDurationEnum.Long__,
                _ => SearchResource.ListRequest.VideoDurationEnum.Any
            };
        }

        if (!string.IsNullOrEmpty(dateFilter) && dateFilter != "any")
        {
            DateTimeOffset? publishedAfter = dateFilter switch
            {
                "today" => DateTimeOffset.UtcNow.AddDays(-1),
                "week" => DateTimeOffset.UtcNow.AddDays(-7),
                "month" => DateTimeOffset.UtcNow.AddMonths(-1),
                "year" => DateTimeOffset.UtcNow.AddYears(-1),
                _ => null
            };

            if (publishedAfter.HasValue)
            {
                // Eski olan PublishedAfter yerine yeni olanı kullanıyoruz
                searchRequest.PublishedAfterDateTimeOffset = publishedAfter; 
            }
        }
        // --------------------------------------------------

        var searchResponse = await searchRequest.ExecuteAsync();
        var results = new List<CommentHunterResult>();

        foreach (var item in searchResponse.Items)
        {
            var commentRequest = youtubeService.CommentThreads.List("snippet");
            commentRequest.VideoId = item.Id.VideoId;
            commentRequest.MaxResults = 50; 
            commentRequest.Order = CommentThreadsResource.ListRequest.OrderEnum.Relevance;

            try
            {
                var commentResponse = await commentRequest.ExecuteAsync();
                int matchCount = 0;
                string? firstMatchedComment = null;

                foreach (var thread in commentResponse.Items)
                {
                    var commentText = thread.Snippet.TopLevelComment.Snippet.TextOriginal.ToLower();
                    
                    bool isMatch = commentKeywordList.Any(kw => commentText.Contains(kw));

                    if (isMatch)
                    {
                        matchCount++;
                        if (firstMatchedComment == null) 
                        {
                            firstMatchedComment = thread.Snippet.TopLevelComment.Snippet.TextDisplay; 
                        }
                    }
                }

                if (matchCount > 0)
                {
                    results.Add(new CommentHunterResult
                    {
                        VideoId = item.Id.VideoId,
                        Title = item.Snippet.Title,
                        ChannelName = item.Snippet.ChannelTitle, // Kanal adını buradan alıyoruz
                        Thumbnail = item.Snippet.Thumbnails.Medium.Url,
                        CommentCount = matchCount,
                        SampleComment = firstMatchedComment
                    });
                }
            }
            catch { }
        }

        if (results.Any())
        {
            var videoIds = string.Join(",", results.Select(r => r.VideoId));
            var statRequest = youtubeService.Videos.List("statistics");
            statRequest.Id = videoIds;
            var statResponse = await statRequest.ExecuteAsync();

            foreach (var statItem in statResponse.Items)
            {
                var res = results.FirstOrDefault(r => r.VideoId == statItem.Id);
                if (res != null)
                {
                    res.ViewCount = statItem.Statistics.ViewCount;
                }
            }
        }

        ViewBag.SearchedMainKeywords = string.Join(", ", mainKeywordList);
        ViewBag.SearchedCommentKeywords = string.Join(", ", commentKeywordList);
        
        return View(results);
    }

    [HttpPost]
    public IActionResult ExportCommentHunterToExcel(List<CommentHunterResult> results)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Yorum Avı Sonuçları");
            worksheet.Cell(1, 1).Value = "Video Başlığı";
            worksheet.Cell(1, 2).Value = "Video URL";
            worksheet.Cell(1, 3).Value = "Kanal Adı";
            worksheet.Cell(1, 4).Value = "İzlenme Sayısı";
            worksheet.Cell(1, 5).Value = "Eşleşen Yorum Sayısı";
            worksheet.Cell(1, 6).Value = "Örnek Yorum";

            // Başlık satırını kalın yapalım
            worksheet.Row(1).Style.Font.Bold = true;

            for (int i = 0; i < results.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = results[i].Title;
                worksheet.Cell(i + 2, 2).Value = "https://www.youtube.com/watch?v=" + results[i].VideoId;
                worksheet.Cell(i + 2, 3).Value = results[i].ChannelName;
                worksheet.Cell(i + 2, 4).Value = results[i].ViewCount?.ToString() ?? "0";
                worksheet.Cell(i + 2, 5).Value = results[i].CommentCount;
                worksheet.Cell(i + 2, 6).Value = results[i].SampleComment;
            }

            worksheet.Columns().AdjustToContents(); 

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "YorumAvcisi_Sonuclari.xlsx");
            }
        }
    }
}