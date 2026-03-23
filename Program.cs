// ============================================================
// Program.cs — Punto de entrada de la aplicación CEPLAN
// Configura: DI, Cookie Auth, Rate Limiting, CSP, MVC
// OWASP: A01 A02 A04 A05 A07 A09
// ============================================================
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using LoginAppCore.Data;
using LoginAppCore.Services;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Repositorios (inyección de dependencias) ──────────────────
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<IMensajeRepository, MensajeRepository>();

// ── Autenticación por Cookies (OWASP A07) ────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.LogoutPath       = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan   = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1));
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly  = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite  = SameSiteMode.Strict;
        options.Cookie.Name      = "__ceplan_auth";
        options.Events           = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/Account"))
                    ctx.Response.Redirect(ctx.RedirectUri);
                else
                    ctx.Response.Redirect("/Account/Login?err=session");
                return Task.CompletedTask;
            }
        };
    });

// ── Rate Limiting (OWASP A04) ─────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("login", cfg =>
    {
        cfg.PermitLimit         = 10;
        cfg.Window              = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit          = 0;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Sesión (para estado temporal) ────────────────────────────
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout  = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1));
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});

// ── HttpContextAccessor ───────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ─────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────

// ── Seed inicial (crea usuario Angel si la BD está vacía) ─────
try
{
    using var scope = app.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<UsuarioRepository>();
    repo.SeedSiVacio();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[STARTUP_SEED] {ex.Message}");
}

// ── Errores ───────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ── CSP y Headers de Seguridad (OWASP A05) ───────────────────
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
        "script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none';");
    h.Append("X-Content-Type-Options",  "nosniff");
    h.Append("X-Frame-Options",         "DENY");
    h.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    h.Append("Permissions-Policy",      "geolocation=(), microphone=(), camera=()");
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ── Rutas ─────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Setup}/{id?}");

app.Run();
