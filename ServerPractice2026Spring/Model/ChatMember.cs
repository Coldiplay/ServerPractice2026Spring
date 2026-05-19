using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ServerPractice2026Spring.Model;

[PrimaryKey(nameof(UserLogin), nameof(ChatId))]
public class ChatMember
{
    [Required, ForeignKey(nameof(Chat))] public ulong ChatId { get; set; }
    [Required, ForeignKey(nameof(User)), MaxLength(40)] public string UserLogin { get; set; } = string.Empty;

    public virtual User User { get; set; } = null!;
    public virtual Chat Chat { get; set; } = null!;
}