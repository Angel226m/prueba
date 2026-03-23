// ============================================================
// Controllers/PerfilController.cs — Gestión de perfil CEPLAN
// Maneja visualización y edición de datos del usuario
// Implementa [Authorize] para cumplir con OWASP A01
// ============================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Services;
using LoginAppCore.Models;

namespace LoginAppCore.Controllers;

/// <summary>
/// Controlador para gestión del perfil de usuario
/// Permite visualizar y editar información personal del usuario autenticado
/// </summary>
[Authorize] // OWASP A01: Control de acceso - Solo usuarios autenticados
public class PerfilController : Controller
{
    private readonly UsuarioRepository _repo;
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructor del controlador de perfil
    /// </summary>
    /// <param name="repo">Repositorio de usuarios para obtener datos del perfil</param>
    /// <param name="config">Configuración de la aplicación</param>
    public PerfilController(UsuarioRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    /// <summary>
    /// GET /Perfil - Muestra la página de perfil del usuario
    /// Obtiene la información del usuario autenticado y la presenta en la vista
    /// </summary>
    /// <returns>Vista del perfil con información del usuario</returns>
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            // Obtener datos del usuario autenticado desde los Claims
            var userIdClaim = User.FindFirstValue("UserId");
            var nombre = User.Identity?.Name ?? "";
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var nombreCompleto = User.FindFirstValue("NombreCompleto") ?? nombre;

            // Pasar información del usuario a la vista a través de ViewBag
            // Esto permite que la vista acceda dinámicamente a los datos del perfil
            ViewBag.NombreCompleto = nombreCompleto;
            ViewBag.Email = email;
            ViewBag.NombreUsuario = nombre;

            // Configuración de sesión para el temporizador
            ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
            ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);

            // Log de acceso al perfil para auditoría de seguridad
            if (!string.IsNullOrEmpty(nombre))
            {
                _repo.RegistrarLog(nombre, "PERFIL_ACCESO", GetIp(), GetUa());
            }

            return View();
        }
        catch (Exception ex)
        {
            // Log del error para diagnóstico y monitoreo
            Console.Error.WriteLine($"[PERFIL_ERROR] {ex.Message}");

            // Redirigir al home en caso de error para evitar pantalla en blanco
            TempData["Error"] = "No se pudo cargar el perfil. Intente nuevamente.";
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// Obtiene la dirección IP del cliente para logging de seguridad
    /// Cumple con OWASP A09: Logging y monitoreo de seguridad insuficiente
    /// </summary>
    /// <returns>Dirección IP del cliente o "desconocida" si no está disponible</returns>
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    /// <summary>
    /// Obtiene el User-Agent del cliente para logging de seguridad
    /// Ayuda a identificar patrones de acceso anómalos
    /// </summary>
    /// <returns>User-Agent del navegador o cadena vacía</returns>
    private string GetUa() => Request.Headers.UserAgent.ToString();
}