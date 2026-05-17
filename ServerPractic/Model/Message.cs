using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerPractic.Model;

public class Message
{
    public Guid Id { get; set; }
    [Required, MaxLength(500)] public string Text { get; set; } = string.Empty;
    [Required, ForeignKey(nameof(Sender)), MaxLength(60)] public string SenderLogin { get; set; } = string.Empty;
    [Required, ForeignKey(nameof(Chat))] public ulong ChatId { get; set; }
    [Required] public DateTimeOffset Timestamp { get; set; }

    public virtual User Sender { get; set; } = null!;
    public virtual Chat Chat { get; set; } = null!;
}