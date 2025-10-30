using Application.Api.Attributes;
using Application.Configuration;
using Application.Core;
using Application.Infrastructure.Database;
using Application.Services;
using Application.Services.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure RazorViewEngine to use custom view location formats
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    // Clear existing locations and define custom ones
    options.ViewLocationFormats.Clear();

    options.ViewLocationFormats.Add("/Views/{1}/{0}.cshtml"); // Look in Views/{controller}/{viewName}.cshtml
    options.ViewLocationFormats.Add("/Views/Shared/{0}.cshtml"); // Fallback to global Shared views

    options.AreaViewLocationFormats.Clear();
    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{1}/{0}.cshtml"); // Look in Areas/{area}/Views/{controller}/{viewName}.cshtml
    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml"); // Prioritize a Shared folder, if there is one
    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{0}.cshtml"); // Fallback to the bare Views folder of the area
    options.AreaViewLocationFormats.Add("/Views/Shared/{0}.cshtml"); // Finally, include the global shared Views
});

// Infrastructure - Database
string? connectionString = builder.Configuration.GetConnectionString("Database");
builder.Services.AddDbContext<DatabaseContext>(options => options.UseSqlServer(connectionString));

// Authentication & Authorization
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Authentication";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.AccessDeniedPath = "/error/403";
        options.LoginPath = "/auth/login";
    });

builder.Services.AddAuthorization();

// Custom strongly-typed settings
builder
    .Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection("EmailSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder
    .Services.AddOptions<AccountConfirmationSettings>()
    .Bind(builder.Configuration.GetSection("AccountConfirmation"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder
    .Services.AddOptions<PasswordResetSettings>()
    .Bind(builder.Configuration.GetSection("PasswordReset"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EmailService>();
builder.Services.AddTransient<EmailConfirmationService>();
builder.Services.AddTransient<PasswordRecoveryService>();
builder.Services.AddTransient<AccountService>();
builder.Services.AddHostedService<ExpiredUserCleanupService>();
builder.Services.AddHostedService<ExpiredAnnouncementCleanupService>();

// Build application and start building middleware pipeline
WebApplication app = builder.Build();

// Migrate database
using (IServiceScope scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}");

app.MapControllerRoute(
        name: "default",
        pattern: "",
        defaults: new
        {
            controller = "Home",
            action = "Index",
            area = "Home",
        }
    )
    .WithStaticAssets();

app.Run();
