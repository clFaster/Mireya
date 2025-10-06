using Microsoft.AspNetCore.Identity;

namespace Mireya.Database.Models;

public class User : IdentityUser
{
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
