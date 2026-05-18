using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ServerPractic.Model;

[PrimaryKey(nameof(Id))]
public class Chat
{
    public ulong Id { get; set; }
    [Required, MaxLength(80), MinLength(1)] public string Title { get; set; } = string.Empty;
}