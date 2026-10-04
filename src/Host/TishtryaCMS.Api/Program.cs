using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Microsoft.OpenApi;
using TishtryaCMS.Api;
using TishtryaCMS.Modules.Comments;
using TishtryaCMS.Modules.Comments.Infrastructure;
using TishtryaCMS.Modules.ContentModules;
using TishtryaCMS.Modules.ContentModules.Infrastructure;
using TishtryaCMS.Modules.EditorTrya;
using TishtryaCMS.Modules.EditorTrya.Infrastructure;
using TishtryaCMS.Modules.FileManager;
using TishtryaCMS.Modules.FileManager.Infrastructure;
using TishtryaCMS.Modules.Forms;
using TishtryaCMS.Modules.Forms.Infrastructure;
using TishtryaCMS.Modules.Identity;
using TishtryaCMS.Modules.Identity.Infrastructure;
using TishtryaCMS.Modules.Qa;
using TishtryaCMS.Modules.Qa.Infrastructure;
using TishtryaCMS.Modules.Settings;
using TishtryaCMS.Modules.Settings.Infrastructure;
using TishtryaCMS.Modules.Tickets;
using TishtryaCMS.Modules.Tickets.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var maxDirectUpload = builder.Configuration.GetValue("FileManager:MaxFileSizeBytes", 5L * 1024 * 1024);
var maxChunkSize = builder.Configuration.GetValue("FileManager:ChunkSizeBytes", 2 * 1024 * 1024);
var maxRequestBody = Math.Max(maxDirectUpload, maxChunkSize + (512 * 1024));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBody;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBody;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TishtryaCMS API",
        Version = "v1",
        Description = "TishtryaCMS modular monolith API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste JWT access token"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ControlCenter", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                 "https://empireiran.com",
                 "https://admin.empireiran.com",
                "http://localhost:5173",
                "http://localhost:4173",
                "http://localhost:8080")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var identityModule = new IdentityModule();
identityModule.Register(builder.Services, builder.Configuration);

var contentModulesModule = new ContentModulesModule();
contentModulesModule.Register(builder.Services, builder.Configuration);

var fileManagerModule = new FileManagerModule();
fileManagerModule.Register(builder.Services, builder.Configuration);

var editorTryaModule = new EditorTryaModule();
editorTryaModule.Register(builder.Services, builder.Configuration);

var qaModule = new QaModule();
qaModule.Register(builder.Services, builder.Configuration);

var commentsModule = new CommentsModule();
commentsModule.Register(builder.Services, builder.Configuration);

var formsModule = new FormsModule();
formsModule.Register(builder.Services, builder.Configuration);

var ticketsModule = new TicketsModule();
ticketsModule.Register(builder.Services, builder.Configuration);

var settingsModule = new SettingsModule();
settingsModule.Register(builder.Services, builder.Configuration);

var app = builder.Build();

await IdentitySeeder.SeedAsync(app.Services);
await ContentModulesSeeder.SeedAsync(app.Services);
await FileManagerSeeder.SeedAsync(app.Services);
await EditorTryaSeeder.SeedAsync(app.Services);
await QaSeeder.SeedAsync(app.Services);
await CommentsSeeder.SeedAsync(app.Services);
await FormsSeeder.SeedAsync(app.Services);
await TicketsSeeder.SeedAsync(app.Services);
await SettingsSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TishtryaCMS API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("ControlCenter");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

FileManagerModule.UseStaticUploads(app);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "TishtryaCMS.Api",
    architecture = "Modular Monolith"
}))
.WithName("Health")
.WithTags("Health");

app.MapGet("/health/db", async (IConfiguration config, CancellationToken cancellationToken) =>
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Connection string 'DefaultConnection' is not configured.");
    }

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT DB_NAME()", connection);
        var databaseName = (string?)await command.ExecuteScalarAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "Healthy",
            database = databaseName
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "Database connection failed",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("HealthDb")
.WithTags("Health");

identityModule.MapEndpoints(app);
contentModulesModule.MapEndpoints(app);
fileManagerModule.MapEndpoints(app);
editorTryaModule.MapEndpoints(app);
qaModule.MapEndpoints(app);
commentsModule.MapEndpoints(app);
formsModule.MapEndpoints(app);
ticketsModule.MapEndpoints(app);
settingsModule.MapEndpoints(app);
app.MapHeaderEndpoints();
app.MapPublicMenuEndpoints();
app.MapPublicBlogGroupEndpoints();
app.MapPublicBlogPostEndpoints();

app.Run();
