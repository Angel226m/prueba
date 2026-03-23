using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Services;

namespace LoginAppCore.Controllers;

[Authorize]
public class PerfilController : Controller
{
    private readonly UsuarioRepository _repo;
    private readonly IConfiguration _config;

    public PerfilController(UsuarioRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var nombre = User.Identity?.Name ?? "";
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var nombreCompleto = User.FindFirstValue("NombreCompleto") ?? nombre;

            ViewBag.NombreCompleto = nombreCompleto;
            ViewBag.Email = email;
            ViewBag.NombreUsuario = nombre;
            ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
            ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);

            if (!string.IsNullOrEmpty(nombre))
                _repo.RegistrarLog(nombre, "PERFIL_ACCESO", GetIp(), GetUa());

            return View();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[PERFIL_ERROR] " + ex.Message);
            TempData["Error"] = "No se pudo cargar el perfil. Intente nuevamente.";
            return RedirectToAction("Index", "Home");
        }
    }

    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    private string GetUa() => Request.Headers.UserAgent.ToString();
}