using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ServerPractic.Model;

[PrimaryKey(nameof(Login))]
public class User
{
    [Required, MaxLength(40), MinLength(4)]
    public string Login { get; set; } = string.Empty;
    
    [Required, MaxLength(512)] public string Password { get; set; } = string.Empty;
}