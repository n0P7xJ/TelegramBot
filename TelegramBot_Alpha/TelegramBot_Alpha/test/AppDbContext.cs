using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TelegramBotDB;Trusted_Connection=True;TrustServerCertificate=True;");
    }
}
