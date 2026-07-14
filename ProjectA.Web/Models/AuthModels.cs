using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Username is required.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

// Shape returned by POST /api/auth/login.
public sealed record LoginResult(string Token, DateTime ExpiresAtUtc, string Username);
