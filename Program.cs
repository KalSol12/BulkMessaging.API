using BulkMessaging.API.Data;
using BulkMessaging.API.Services;
using Microsoft.EntityFrameworkCore;
using AfroMessage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS for React
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                return false;
            })
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();