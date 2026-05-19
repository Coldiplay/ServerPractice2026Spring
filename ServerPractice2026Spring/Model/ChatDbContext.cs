using Microsoft.EntityFrameworkCore;

namespace ServerPractice2026Spring.Model;

public class ChatDbContext : DbContext
{
    public ChatDbContext()
    {
        Database.EnsureCreated();
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }

    private const string ConnectionString = "Server=192.168.200.13;Database=PracticChatDb;user=student;password=student";
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
    }
}