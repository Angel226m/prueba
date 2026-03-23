// ============================================================
// Controllers/BuscarController.cs — Sistema de búsqueda CEPLAN
// Permite buscar contenido, usuarios y funcionalidades
// Requiere [Authorize] — OWASP A01
// ============================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Services;
using LoginAppCore.Data;
using LoginAppCore.Models;

namespace LoginAppCore.Controllers;

/// <summary>
/// Controlador del sistema de búsqueda global de CEPLAN
/// Permite buscar usuarios, mensajes y contenido del sistema
/// </summary>
[Authorize] // OWASP A01: Control de acceso - Solo usuarios autenticados
public class BuscarController : Controller
{
    private readonly UsuarioRepository _usuarios;
    private readonly IMensajeRepository _mensajes;
    private readonly IConfiguration _config;

    /// <summary>
    /// Constructor del controlador de búsqueda
    /// </summary>
    /// <param name="usuarios">Repositorio de usuarios para búsquedas</param>
    /// <param name="mensajes">Repositorio de mensajes para búsquedas</param>
    /// <param name="config">Configuración de la aplicación</param>
    public BuscarController(UsuarioRepository usuarios, IMensajeRepository mensajes, IConfiguration config)
    {
        _usuarios = usuarios;
        _mensajes = mensajes;
        _config = config;
    }

    /// <summary>
    /// GET /Buscar - Página principal de búsqueda
    /// Muestra interface de búsqueda y resultados si hay query
    /// </summary>
    /// <param name="q">Query de búsqueda (opcional)</param>
    /// <returns>Vista de búsqueda con resultados</returns>
    [HttpGet]
    public async Task<IActionResult> Index(string? q = null)
    {
        try
        {
            // Configuración del layout y sesión
            ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
            ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);

            // Preparar modelo de búsqueda
            ViewBag.Query = q?.Trim();
            ViewBag.TieneResultados = !string.IsNullOrWhiteSpace(q);

            // Si hay query, realizar búsqueda
            if (!string.IsNullOrWhiteSpace(q) && q.Trim().Length >= 2)
            {
                var userId = GetUserId();

                // Buscar en múltiples fuentes (simulado por ahora)
                ViewBag.ResultadosUsuarios = await BuscarUsuarios(q.Trim());
                ViewBag.ResultadosMensajes = await BuscarMensajes(q.Trim(), userId);

                // Log de búsqueda para analytics
                _usuarios.RegistrarLog(User.Identity?.Name ?? "?",
                    $"SEARCH:{q.Trim()}", GetIp(), GetUa());
            }

            return View();
        }
        catch (Exception ex)
        {
            // Log del error para diagnóstico
            Console.Error.WriteLine($"[SEARCH_ERROR] {ex.Message}");

            // En caso de error, mostrar página vacía
            ViewBag.Error = "Error al realizar la búsqueda. Intente nuevamente.";
            return View();
        }
    }

    /// <summary>
    /// Busca usuarios por nombre o email (simulado)
    /// En una implementación real, esto haría query a la base de datos
    /// </summary>
    /// <param name="query">Término de búsqueda</param>
    /// <returns>Lista de usuarios que coinciden</returns>
    private async Task<List<UsuarioItem>> BuscarUsuarios(string query)
    {
        // Simulación de búsqueda de usuarios
        // En implementación real: query a base de datos con LIKE o full-text search
        await Task.Delay(50); // Simula latencia de BD

        var resultados = new List<UsuarioItem>();

        // Agregar algunos resultados simulados si coincide con "angel" o "admin"
        if (query.ToLower().Contains("angel") || query.ToLower().Contains("admin"))
        {
            resultados.Add(new UsuarioItem
            {
                Id = 1,
                NombreCompleto = "Angel Quispe Mamani",
                NombreUsuario = "angel",
                Email = "angel@ceplan.gob.pe"
            });
        }

        return resultados;
    }

    /// <summary>
    /// Busca en mensajes del usuario actual (simulado)
    /// En una implementación real, buscaría en el contenido de mensajes
    /// </summary>
    /// <param name="query">Término de búsqueda</param>
    /// <param name="userId">ID del usuario actual</param>
    /// <returns>Lista de mensajes que coinciden</returns>
    private async Task<List<Mensaje>> BuscarMensajes(string query, int userId)
    {
        try
        {
            // En implementación real: búsqueda full-text en asunto y cuerpo de mensajes
            var mensajes = await _mensajes.ObtenerBandejaEntradaAsync(userId);

            return mensajes
                .Where(m => m.Asunto.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           m.Cuerpo.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(10) // Limitar resultados
                .ToList();
        }
        catch
        {
            return new List<Mensaje>(); // En caso de error, devolver lista vacía
        }
    }

    /// <summary>
    /// Obtiene el ID del usuario actual desde los Claims
    /// </summary>
    /// <returns>ID del usuario autenticado</returns>
    private int GetUserId()
    {
        var userIdClaim = User.FindFirstValue("UserId");
        return userIdClaim != null && int.TryParse(userIdClaim, out int uid) ? uid : 0;
    }

    /// <summary>
    /// Obtiene la dirección IP para logging de auditoría
    /// </summary>
    /// <returns>IP del cliente</returns>
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    /// <summary>
    /// Obtiene el User-Agent para análisis de patrones
    /// </summary>
    /// <returns>User-Agent del navegador</returns>
    private string GetUa() => Request.Headers.UserAgent.ToString();
}