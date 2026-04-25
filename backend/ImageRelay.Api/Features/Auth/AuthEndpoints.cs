using System.Security.Claims;
using ImageRelay.Api.Features.Common;

namespace ImageRelay.Api.Features.Auth;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username);
public record ChangePasswordRequest(string OldPassword, string NewPassword);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/auth");

        g.MapPost("/login", async (
            [FromBody] LoginRequest req,
            AppDbContext db,
            IPasswordHasher hasher,
            IJwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return ApiResponse.BadRequest("username and password required");

            var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
                return ApiResponse.Unauthorized();

            return ApiResponse.Ok(new LoginResponse(jwt.Issue(user), user.Username));
        }).AllowAnonymous();

        g.MapGet("/me", (ClaimsPrincipal user) => ApiResponse.Ok(new
        {
            username = user.Identity?.Name ?? user.FindFirstValue("unique_name"),
            id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")
        })).RequireAuthorization();

        g.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest req,
            ClaimsPrincipal principal,
            AppDbContext db,
            IPasswordHasher hasher) =>
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return ApiResponse.BadRequest("new password too short");

            var idStr = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idStr, out var id)) return ApiResponse.Unauthorized();

            var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return ApiResponse.Unauthorized();
            if (!hasher.Verify(req.OldPassword, user.PasswordHash))
                return ApiResponse.BadRequest("old password incorrect");

            user.PasswordHash = hasher.Hash(req.NewPassword);
            await db.SaveChangesAsync();
            return ApiResponse.Ok();
        }).RequireAuthorization();
    }
}
