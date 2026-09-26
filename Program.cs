using BulkMessaging.API.Data;
using BulkMessaging.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using AfroMessage;
using BulkMessaging.API.Models;
using BulkMessaging.API.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BulkMessaging.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BulkMessaging.Client";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var tokenMinutes = builder.Configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 30;
var bootstrapAdminEmail = builder.Configuration["BootstrapAdmin:Email"];
var bootstrapAdminPassword = builder.Configuration["BootstrapAdmin:Password"];

if (string.IsNullOrWhiteSpace(jwtSigningKey) ||
    Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt:SigningKey with at least 32 UTF-8 bytes of random secret material.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Issuer and Jwt:Audience must be non-empty.");
}

if (tokenMinutes is < 5 or > 1440)
{
    throw new InvalidOperationException(
        "Jwt:AccessTokenMinutes must be between 5 and 1440.");
}

if (string.IsNullOrWhiteSpace(bootstrapAdminEmail) ||
    !new EmailAddressAttribute().IsValid(bootstrapAdminEmail))
{
    throw new InvalidOperationException(
        "Configure BootstrapAdmin:Email with a valid initial administrator email.");
}

if (string.IsNullOrWhiteSpace(bootstrapAdminPassword) ||
    bootstrapAdminPassword.Length < 12 ||
    !bootstrapAdminPassword.Any(char.IsUpper) ||
    !bootstrapAdminPassword.Any(char.IsLower) ||
    !bootstrapAdminPassword.Any(char.IsDigit) ||
    !bootstrapAdminPassword.Any(character => !char.IsLetterOrDigit(character)))
{
    throw new InvalidOperationException(
        "BootstrapAdmin:Password must be at least 12 characters and include uppercase, lowercase, numeric, and non-alphanumeric characters.");
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenSecurityStamp = context.Principal?
                    .FindFirstValue("AspNet.Identity.SecurityStamp");
                var user = userId is null
                    ? null
                    : await userManager.FindByIdAsync(userId);

                if (user is null ||
                    tokenSecurityStamp is null ||
                    !string.Equals(
                        user.SecurityStamp,
                        tokenSecurityStamp,
                        StringComparison.Ordinal))
                {
                    context.Fail("The access token has been revoked.");
                }
            },
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PasswordChanged", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new PasswordChangedRequirement());
    });
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<IAuthorizationHandler, PasswordChangedAuthorizationHandler>();

// CORS for React
var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];
var allowedOrigins = builder.Environment.IsDevelopment()
    ? configuredOrigins
        .Concat(["http://localhost:5173", "http://127.0.0.1:5173"])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()
    : configuredOrigins;

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Configure Cors:AllowedOrigins with the deployed frontend origin.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---- SMTP ----
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<SmtpMessageSender>();

// ---- AfroMessage (SMS) ----
builder.Services.Configure<AfroMessageConfig>(
    builder.Configuration.GetSection("AfroMessage"));

// Register the AfroMessage HTTP client + services
builder.Services.AddAfroMessage(
    builder.Configuration.GetSection("AfroMessage").Get<AfroMessageConfig>()!);

// ✅ THIS LINE WAS MISSING — register SmsMessageSender
builder.Services.AddScoped<SmsMessageSender>();

// ---- Dispatcher ----
builder.Services.AddScoped<MessageSenderDispatcher>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create the {role} role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
            }
        }
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var administrator = await userManager.FindByEmailAsync(bootstrapAdminEmail);
    if (administrator is null)
    {
        administrator = new ApplicationUser
        {
            UserName = bootstrapAdminEmail,
            Email = bootstrapAdminEmail,
            DisplayName = builder.Configuration["BootstrapAdmin:DisplayName"]?.Trim()
                ?? bootstrapAdminEmail,
            MustChangePassword = false,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
        var createResult = await userManager.CreateAsync(administrator, bootstrapAdminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to bootstrap the initial administrator: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
        }
    }

    if (!await userManager.IsInRoleAsync(administrator, "Admin"))
    {
        var roleResult = await userManager.AddToRoleAsync(administrator, "Admin");
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to assign the Admin role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();



// postgresql://neondb_owner:npg_fbY0MPSO1ezt@ep-quiet-cake-b5s24agd-pooler.c-7.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require