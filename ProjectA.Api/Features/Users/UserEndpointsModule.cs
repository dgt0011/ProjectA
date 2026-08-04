using ProjectA.Api.Features.Users.ChangePassword;
using ProjectA.Api.Features.Users.CreateUser;
using ProjectA.Api.Features.Users.DeleteUser;
using ProjectA.Api.Features.Users.GetUserById;
using ProjectA.Api.Features.Users.GetUserList;

namespace ProjectA.Api.Features.Users;

public static class UserEndpointsModule
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // Every user-management endpoint requires an authenticated caller - there is no
        // public self-registration endpoint, so new accounts only ever get created by someone
        // who is already logged in (the "admin-provisioned" model).
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGetUserList();
        group.MapGetUserById();
        group.MapCreateUser();
        group.MapDeleteUser();
        group.MapChangePassword();
    }
}
