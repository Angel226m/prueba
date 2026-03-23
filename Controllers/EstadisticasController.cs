// ============================================================
// Controllers/EstadisticasController.cs — Métricas y analytics CEPLAN
// Proporciona insights de uso y seguridad del sistema
// Requiere [Authorize] — OWASP A01
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Services;
using LoginAppCore.Models;

namespace LoginAppCore.Controllers;

/// <summary>
/// Controlador de estadísticas y métricas del sistema CEPLAN
/// Proporciona dashboards de análisis de uso y seguridad
/// </summary>
[Authorize] // OWASP A01: Solo usuarios autenticados pueden acceder
public class EstadisticasController : Controller
{
    private readonly UsuarioRepository _repo;
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructor del controlador de estadísticas
    /// </summary>
    /// <param name="repo">Repositorio de usuarios para obtener métricas</param>
    /// <param name="config">Configuración de la aplicación</param>
    public EstadisticasController(UsuarioRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    /// <summary>
    /// GET /Estadisticas - Dashboard de métricas del sistema
    /// Muestra estadísticas de uso, seguridad y performance
    /// </summary>
    /// <returns>Vista con métricas y gráficos estadísticos</returns>
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            // Obtener estadísticas desde el repositorio
            ViewBag.Stats = _repo.ObtenerEstadisticasHoy();

            // Configuración de sesión para el layout
            ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
            ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);

            // Log de acceso para auditoría
            var userName = User.Identity?.Name ?? "desconocido";
            _repo.RegistrarLog(userName, "STATS_ACCESS", GetIp(), GetUa());

            return View();
        }
        catch (Exception ex)
        {
            // Log del error para diagnóstico
            Console.Error.WriteLine($"[STATS_ERROR] {ex.Message}");

            // Redirigir al dashboard principal en caso de error
            TempData["Error"] = "No se pudieron cargar las estadísticas. Intente nuevamente.";
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// Obtiene la dirección IP del cliente para logging de auditoría
    /// Implementa OWASP A09: Logging y monitoreo de seguridad
    /// </summary>
    /// <returns>IP del cliente o "desconocida"</returns>
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    /// <summary>
    /// Obtiene el User-Agent para análisis de patrones de acceso
    /// </summary>
    /// <returns>User-Agent del navegador</returns>
    private string GetUa() => Request.Headers.UserAgent.ToString();
}