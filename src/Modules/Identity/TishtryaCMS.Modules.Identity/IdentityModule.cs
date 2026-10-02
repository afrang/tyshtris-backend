using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TishtryaCMS.Modules.Identity.Application;
using TishtryaCMS.Modules.Identity.Domain;
using TishtryaCMS.Modules.Identity.Infrastructure;
using TishtryaCMS.Modules.Identity.Options;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public const string AdminOnlyPolicy = "AdminOnly";

    public string Name => "Identity";

    public void Register(IServiceCollection services)
    {
        // Registered via Register(services, configuration) from the host.
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.AddSingleton<IValidateOptions<OtpOptions>, OtpOptionsValidator>();

        var smtpHost = configuration.GetSection(SmtpOptions.SectionName)["Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            services.AddTransient<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddTransient<IEmailSender, ConsoleLoggerEmailSender>();
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<PasswordHasher<User>>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<OtpCodeService>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminOnlyPolicy, policy =>
                policy.RequireRole(Roles.Admin, Roles.SuperAdmin));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapAuthEndpoints(endpoints);
        MapUserEndpoints(endpoints);
    }

    private static void MapAuthEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await authService.RegisterAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Accepted();
            })
            .AllowAnonymous()
            .WithName("Register");

        group.MapPost("/verify-otp", async (VerifyOtpRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await authService.VerifyEmailOtpAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("VerifyOtp");

        group.MapPost("/resend-otp", async (ResendOtpRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await authService.ResendOtpAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Accepted();
            })
            .AllowAnonymous()
            .WithName("ResendOtp");

        group.MapPost("/login", async (LoginRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (response, error) = await authService.LoginAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("Login");

        group.MapPost("/request-login-otp", async (RequestLoginOtpRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (_, statusCode) = await authService.RequestLoginOtpAsync(request, cancellationToken);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .WithName("RequestLoginOtp");

        group.MapPost("/login-otp", async (LoginOtpRequest request, AuthService authService, CancellationToken cancellationToken) =>
            {
                var (response, error) = await authService.LoginOtpAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("LoginOtp");

        group.MapGet("/me", async (
            HttpContext httpContext,
            UserService userService,
            CancellationToken cancellationToken) =>
            {
                var user = httpContext.User;
                if (user.Identity?.IsAuthenticated != true)
                {
                    return Results.Unauthorized();
                }

                var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? user.FindFirstValue("sub");
                if (!Guid.TryParse(idValue, out var userId))
                {
                    return Results.Ok(new
                    {
                        email = user.FindFirst("email")?.Value
                                ?? user.FindFirst(ClaimTypes.Email)?.Value,
                        role = user.FindFirst(ClaimTypes.Role)?.Value,
                        displayName = user.FindFirst("display_name")?.Value
                    });
                }

                var (response, error, statusCode) = await userService.GetByIdAsync(userId, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("Me");

        group.MapPut("/profile", async (
            UpdateOwnProfileRequest request,
            HttpContext httpContext,
            UserService userService,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await userService.UpdateOwnProfileAsync(
                    httpContext.User,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .RequireAuthorization()
            .WithName("UpdateOwnProfile");

        group.MapPut("/password", async (
            ChangeOwnPasswordRequest request,
            HttpContext httpContext,
            UserService userService,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await userService.ChangeOwnPasswordAsync(
                    httpContext.User,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .RequireAuthorization()
            .WithName("ChangeOwnPassword");
    }

    private static void MapUserEndpoints(IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/api/admin/users")
            .WithTags("Users")
            .RequireAuthorization(AdminOnlyPolicy);

        users.MapGet("/", async (UserService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListUsers");

        users.MapGet("/{id:guid}", async (Guid id, UserService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetUserById");

        users.MapPost("/", async (CreateUserRequest request, UserService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/users/{response!.Id}", response);
            })
            .WithName("CreateUser");

        users.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserProfileRequest request,
            HttpContext httpContext,
            UserService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateProfileAsync(
                    id,
                    request,
                    httpContext.User,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateUser");

        users.MapPut("/{id:guid}/role", async (
            Guid id,
            ChangeUserRoleRequest request,
            HttpContext httpContext,
            UserService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ChangeRoleAsync(
                    id,
                    request,
                    httpContext.User,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ChangeUserRole");

        users.MapPut("/{id:guid}/password", async (
            Guid id,
            SetUserPasswordRequest request,
            HttpContext httpContext,
            UserService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.SetPasswordAsync(
                    id,
                    request,
                    httpContext.User,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("SetUserPassword");

        users.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            UserService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, httpContext.User, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteUser");
    }
}
