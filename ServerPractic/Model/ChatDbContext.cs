using Microsoft.EntityFrameworkCore;

namespace ServerPractic.Model;

public class ChatDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }

    private const string ConnectionString = "Server=192.168.200.13;Database=PracticChatDb;user=student;password=student";
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Database.EnsureCreated();
        base.OnModelCreating(modelBuilder);
    }
}