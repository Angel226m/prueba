// ============================================================
// Controllers/AccountController.cs — Autenticación CEPLAN
// Sistema de bloqueo mejorado: 4 intentos máximo, 20 minutos lockout
// Sistema de hash avanzado con pepper y validaciones estrictas
// ============================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LoginAppCore.Models;
using LoginAppCore.Services;

namespace LoginAppCore.Controllers;

public class AccountController : Controller
{
    private const string MSG_ERROR     = "Usuario o contraseña incorrectos.";
    private const string MSG_INACTIVO  = "Cuenta inactiva. Contacta al administrador.";
    private const string MSG_BLOQUEADO = "Cuenta temporalmente bloqueada por múltiples intentos fallidos.";

    private readonly UsuarioRepository _repo;
    private readonly IConfiguration    _config;

    public AccountController(UsuarioRepository repo, IConfiguration config)
    {
        _repo   = repo;
        _config = config;
    }

    // ── GET /Account/Setup ──────────────────────────────────────
    [HttpGet]
    public IActionResult Setup(string? nombre = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        try
        {
            if (_repo.ExistenUsuarios())
            {
                ViewBag.NombreUsuario = nombre;
                return View();
            }
        }
        catch
        {
            ViewBag.Error = "No se pudo conectar a la base de datos.";
        }

        ViewBag.NombreUsuario = nombre;
        return View(new SetupViewModel());
    }

    // ── POST /Account/Setup ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Setup(SetupViewModel model)
    {
        if (_repo.ExistenUsuarios())
            return RedirectToAction("Login");

        if (!SecurityHelper.IsValidInput(model.NombreUsuario, out string usuario))
            ModelState.AddModelError(nameof(model.NombreUsuario), "Nombre de usuario inválido.");

        if (!SecurityHelper.IsValidEmail(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Correo electrónico inválido.");

        if (!SecurityHelper.IsStrongPassword(model.Password))
            ModelState.AddModelError(nameof(model.Password),
                "La contraseña debe tener 8-128 caracteres, incluir mayúscula, minúscula, número y carácter especial. No debe contener patrones comunes débiles.");

        if (!ModelState.IsValid) return View(model);

        try
        {
            _repo.CrearUsuario(usuario, model.Email.Trim().ToLower(), model.Password);
            TempData["Exito"] = $"Usuario '{SecurityHelper.HtmlEncode(usuario)}' creado correctamente.";
            return RedirectToAction("Login");
        }
        catch (ArgumentException ex)
        {
            // Error de validación de contraseña del nuevo sistema de hash
            Console.Error.WriteLine($"[SETUP_VALIDATION_ERROR] {ex.Message}");
            ModelState.AddModelError(nameof(model.Password), ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SETUP_ERROR] {ex}");
            ModelState.AddModelError(string.Empty, "Error al crear el usuario. Verifique sus datos e intente nuevamente.");
            return View(model);
        }
    }

    // ── GET /Account/Login ──────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? err = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (err == "session")
            ViewBag.Aviso = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";

        ViewBag.ReturnUrl             = returnUrl;
        ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
        ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);
        return View(new LoginViewModel());
    }

    // ── POST /Account/Login ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl             = returnUrl;
        ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
        ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);

        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, MSG_ERROR);
            return View(model);
        }

        if (!SecurityHelper.IsValidInput(model.Credencial, out string credencial) ||
            string.IsNullOrEmpty(model.Password) || model.Password.Length > 128)
        {
            ModelState.AddModelError(string.Empty, MSG_ERROR);
            return View(model);
        }

        try
        {
            var usuario = _repo.ObtenerPorCredencial(credencial);

            // Usuario no encontrado
            if (usuario == null)
            {
                SecurityHelper.VerifyPassword("dummy", "dGVzdA==", "dGVzdA==");
                _repo.RegistrarLog("desconocido", "LOGIN_FAIL", GetIp(), GetUa());
                ModelState.AddModelError(string.Empty, MSG_ERROR);
                return View(model);
            }

            if (!usuario.Activo)
            {
                _repo.RegistrarLog(usuario.NombreUsuario, "LOGIN_FAIL_INACTIVO", GetIp(), GetUa());
                ModelState.AddModelError(string.Empty, MSG_INACTIVO);
                return View(model);
            }

            // ── Verificar si YA está bloqueado ANTES de intentar ──
            // Muestra mensaje de bloqueo si la cuenta está temporalmente bloqueada
            if (usuario.EstaBloqueado)
            {
                _repo.RegistrarLog(usuario.NombreUsuario, "LOGIN_BLOQUEADO_ACTIVO", GetIp(), GetUa());

                // Calcular tiempo restante de bloqueo
                var tiempoRestante = usuario.BloqueadoHasta!.Value - DateTime.UtcNow;
                var minutosRestantes = (int)Math.Ceiling(tiempoRestante.TotalMinutes);

                ViewBag.Bloqueado = true;
                ViewBag.TiempoRestante = minutosRestantes;
                return View(model);
            }

            // Verificar contraseña con sistema de hash mejorado
            if (!SecurityHelper.VerifyPassword(model.Password, usuario.PasswordHash, usuario.PasswordSalt))
            {
                // Incrementar contador de fallos
                _repo.RegistrarIntentoFallido(usuario.Id);

                // Re-consultar INMEDIATAMENTE para obtener el contador actualizado
                var recheck = _repo.ObtenerPorCredencial(credencial);
                var intentosActuales = recheck?.IntentosFallidos ?? 0;

                _repo.RegistrarLog(usuario.NombreUsuario, $"LOGIN_FAIL_ATTEMPT_{intentosActuales}", GetIp(), GetUa());

                if (recheck?.EstaBloqueado == true)
                {
                    // Cuenta bloqueada en este intento - mostrar pantalla de bloqueo
                    _repo.RegistrarLog(usuario.NombreUsuario, "LOGIN_BLOQUEADO_NUEVO", GetIp(), GetUa());
                    ViewBag.Bloqueado = true;
                    ViewBag.TiempoRestante = 20; // Duración completa del bloqueo
                    return View(model);
                }

                // Aún no está bloqueado - mostrar error genérico con advertencia sutil
                var maxIntentos = 4; // Máximo de intentos permitidos
                var intentosRestantes = maxIntentos - intentosActuales;

                if (intentosRestantes <= 1)
                {
                    // Último intento antes del bloqueo
                    ModelState.AddModelError(string.Empty,
                        $"{MSG_ERROR} Te queda {intentosRestantes} intento antes del bloqueo temporal de 20 minutos.");
                }
                else if (intentosRestantes <= 2)
                {
                    // Advertencia cuando quedan pocos intentos
                    ModelState.AddModelError(string.Empty,
                        $"{MSG_ERROR} Te quedan {intentosRestantes} intentos antes del bloqueo temporal.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, MSG_ERROR);
                }

                return View(model);
            }

            // ── Login exitoso ───────────────────────────────────
            _repo.RegistrarAccesoExitoso(usuario.Id);
            _repo.RegistrarLog(usuario.NombreUsuario, "LOGIN_OK", GetIp(), GetUa());

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  usuario.NombreUsuario),
                new(ClaimTypes.Email, usuario.Email),
                new("UserId",         usuario.Id.ToString()),
                new("NombreCompleto", usuario.NombreCompleto.Length > 0
                    ? usuario.NombreCompleto : usuario.NombreUsuario),
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.Recordar,
                ExpiresUtc   = model.Recordar
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddMinutes(
                        _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 20))
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LOGIN_EXCEPTION] {ex}");
            ModelState.AddModelError(string.Empty, "Ocurrió un error. Intenta nuevamente.");
            return View(model);
        }
    }

    // ── POST /Account/Logout ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var nombre = User.Identity?.Name ?? "desconocido";
        _repo.RegistrarLog(nombre, "LOGOUT", GetIp(), GetUa());
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // ── POST /Account/ExtenderSesion ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ExtenderSesion()
    {
        var result = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (result?.Principal == null)
            return Unauthorized();

        var authProps = new AuthenticationProperties
        {
            IsPersistent = result.Properties?.IsPersistent ?? false,
            ExpiresUtc   = DateTimeOffset.UtcNow.AddMinutes(
                _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1))
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Principal, authProps);

        _repo.RegistrarLog(User.Identity?.Name ?? "?", "SESSION_EXTENDED", GetIp(), GetUa());
        return Ok(new { extendido = true });
    }

    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    private string GetUa() => Request.Headers.UserAgent.ToString();
}