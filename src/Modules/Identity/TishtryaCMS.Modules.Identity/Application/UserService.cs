using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Identity.Domain;
using TishtryaCMS.Modules.Identity.Infrastructure;

namespace TishtryaCMS.Modules.Identity.Application;

public sealed class UserService(
    IdentityDbContext db,
    PasswordHasher<User> passwordHasher)
{
    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserResponse(x.Id, x.Email, x.DisplayName, x.Role, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return (null, "User not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(user), null, StatusCodes.Status200OK);
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return (null, "Password must be at least 6 characters.", StatusCodes.Status400BadRequest);
            }

            if (!Roles.IsAssignable(request.Role))
            {
                return (null, "Role must be Admin or User.", StatusCodes.Status400BadRequest);
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            {
                return (null, "A user with this email already exists.", StatusCodes.Status409Conflict);
            }

            var placeholder = User.Create(email, "pending", request.Role, request.DisplayName);
            var hash = passwordHasher.HashPassword(placeholder, request.Password);
            var user = User.Create(email, hash, request.Role, request.DisplayName);

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(user), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> UpdateProfileAsync(
        Guid id,
        UpdateUserProfileRequest request,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user is null)
            {
                return (null, "User not found.", StatusCodes.Status404NotFound);
            }

            if (!CanManageUser(actor, user))
            {
                return (null, "You cannot modify this user.", StatusCodes.Status403Forbidden);
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Email == email && x.Id != id, cancellationToken))
            {
                return (null, "A user with this email already exists.", StatusCodes.Status409Conflict);
            }

            user.UpdateProfile(email, request.DisplayName);

            if (request.IsActive.HasValue && Roles.IsAdmin(GetRole(actor)))
            {
                if (IsSelf(actor, user.Id) && !request.IsActive.Value)
                {
                    return (null, "You cannot deactivate your own account.", StatusCodes.Status400BadRequest);
                }

                if (user.Role == Roles.SuperAdmin && !request.IsActive.Value)
                {
                    return (null, "SuperAdmin cannot be deactivated.", StatusCodes.Status400BadRequest);
                }

                user.SetActive(request.IsActive.Value);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(user), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> ChangeRoleAsync(
        Guid id,
        ChangeUserRoleRequest request,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Roles.IsAssignable(request.Role))
            {
                return (null, "Role must be Admin or User.", StatusCodes.Status400BadRequest);
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user is null)
            {
                return (null, "User not found.", StatusCodes.Status404NotFound);
            }

            if (user.Role == Roles.SuperAdmin)
            {
                return (null, "SuperAdmin role cannot be changed.", StatusCodes.Status400BadRequest);
            }

            if (IsSelf(actor, user.Id))
            {
                return (null, "You cannot change your own role.", StatusCodes.Status400BadRequest);
            }

            user.ChangeRole(request.Role);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(user), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> SetPasswordAsync(
        Guid id,
        SetUserPasswordRequest request,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return ("Password must be at least 6 characters.", StatusCodes.Status400BadRequest);
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return ("User not found.", StatusCodes.Status404NotFound);
        }

        if (!CanManageUser(actor, user))
        {
            return ("You cannot modify this user.", StatusCodes.Status403Forbidden);
        }

        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> UpdateOwnProfileAsync(
        ClaimsPrincipal actor,
        UpdateOwnProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return (null, "Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        return await UpdateProfileAsync(
            userId.Value,
            new UpdateUserProfileRequest(request.Email, request.DisplayName, IsActive: null),
            actor,
            cancellationToken);
    }

    public async Task<(string? Error, int StatusCode)> ChangeOwnPasswordAsync(
        ClaimsPrincipal actor,
        ChangeOwnPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return ("Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return ("New password must be at least 6 characters.", StatusCodes.Status400BadRequest);
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return ("User not found.", StatusCodes.Status404NotFound);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            return ("Current password is incorrect.", StatusCodes.Status400BadRequest);
        }

        user.SetPasswordHash(passwordHasher.HashPassword(user, request.NewPassword));
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(
        Guid id,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return ("User not found.", StatusCodes.Status404NotFound);
        }

        if (user.Role == Roles.SuperAdmin)
        {
            return ("SuperAdmin cannot be deleted.", StatusCodes.Status400BadRequest);
        }

        if (IsSelf(actor, user.Id))
        {
            return ("You cannot delete your own account.", StatusCodes.Status400BadRequest);
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAtUtc);

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? GetRole(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role);

    private static bool IsSelf(ClaimsPrincipal actor, Guid userId) =>
        GetUserId(actor) == userId;

    private static bool CanManageUser(ClaimsPrincipal actor, User target)
    {
        if (Roles.IsAdmin(GetRole(actor)))
        {
            return true;
        }

        return IsSelf(actor, target.Id);
    }
}
