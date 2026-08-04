using System.ComponentModel.DataAnnotations;

namespace ProjectA.Web.Models;

public sealed class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

// Shape returned by POST api/users/change-password - never includes DateCreated since that
// endpoint's response is only ever used to confirm the change (id/username/when), not to
// redisplay the account like UserDto does on the Users list.
public sealed class ChangePasswordResult
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
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

// Self-service - deliberately has no Username/Id property. The account changed is always
// whoever is currently signed in (resolved server-side from the bearer token's own claims),
// never a value posted from this form.
public sealed class ChangePasswordInput
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The confirmation does not match the new password.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
