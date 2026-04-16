using Microsoft.EntityFrameworkCore;
using YTReklamAraci.Models;

namespace YTReklamAraci.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<SearchCache> SearchCaches { get; set; }
}