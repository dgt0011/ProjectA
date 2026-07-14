using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

public sealed class UserInput
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
