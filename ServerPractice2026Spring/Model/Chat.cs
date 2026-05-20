using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ServerPractice2026Spring.Model;

[PrimaryKey(nameof(Id))]
public class Chat
{
    public ulong Id { get; set; }
    [Required, MaxLength(80), MinLength(1)] public string Title { get; set; } = string.Empty;
    
    
    public virtual ICollection<User> Users { get; set; } = [];
}