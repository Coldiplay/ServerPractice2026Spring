using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ServerPractic.Model;

[PrimaryKey(nameof(UserId), nameof(ChatId))]
public class ChatMember
{
    [Required, ForeignKey(nameof(Chat))] public ulong ChatId { get; set; }
    [Required, ForeignKey(nameof(User))] public ulong UserId { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Chat Chat { get; set; } = null!;
}