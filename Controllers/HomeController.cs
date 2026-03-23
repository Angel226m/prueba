// ============================================================
// Controllers/HomeController.cs — Dashboard principal CEPLAN
// Controllers/MensajesController.cs — Mensajería interna
// Requieren [Authorize] — OWASP A01
// ============================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Data;
using LoginAppCore.Models;
using LoginAppCore.Services;

namespace LoginAppCore.Controllers;

// ══════════════════════════════════════════════════════════════
//  HOME — Dashboard
// ══════════════════════════════════════════════════════════════
[Authorize]
public class HomeController : Controller
{
    private readonly UsuarioRepository  _usuarios;
    private readonly IMensajeRepository _mensajes;
    private readonly IConfiguration     _config;

    public HomeController(UsuarioRepository usuarios, IMensajeRepository mensajes, IConfiguration config)
    {
        _usuarios = usuarios;
        _mensajes = mensajes;
        _config   = config;
    }

    public async Task<IActionResult> Index()
    {
        // Estadísticas de hoy
        try { ViewBag.Stats = _usuarios.ObtenerEstadisticasHoy(); }
        catch { ViewBag.Stats = null; }

        // Mensajes recientes
        var userIdClaim = User.FindFirstValue("UserId");
        if (userIdClaim is not null && int.TryParse(userIdClaim, out int uid))
        {
            try
            {
                ViewBag.MensajesRecientes = (await _mensajes.ObtenerBandejaEntradaAsync(uid)).Take(5).ToList();
                ViewBag.NoLeidos          = await _mensajes.ContarNoLeidosAsync(uid);
            }
            catch
            {
                ViewBag.MensajesRecientes = null;
                ViewBag.NoLeidos          = 0;
            }
        }

        ViewBag.Nombre                = User.Identity!.Name;
        ViewBag.Email                 = User.FindFirstValue(ClaimTypes.Email);
        ViewBag.NombreCompleto        = User.FindFirstValue("NombreCompleto");
        ViewBag.SessionWarningSeconds = _config.GetValue<int>("AppSettings:SessionWarningSeconds", 30);
        ViewBag.SessionTimeoutMinutes = _config.GetValue<int>("AppSettings:SessionTimeoutMinutes", 1);
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

// ══════════════════════════════════════════════════════════════
//  MENSAJES — Bandeja, Enviados, Detalle, Nuevo
// ══════════════════════════════════════════════════════════════
[Authorize]
public class MensajesController : Controller
{
    private readonly IMensajeRepository _repo;

    public MensajesController(IMensajeRepository repo) => _repo = repo;

    // GET /Mensajes
    public async Task<IActionResult> Index()
    {
        var uid = GetUserId();
        return View(new BandejaViewModel
        {
            Mensajes = (await _repo.ObtenerBandejaEntradaAsync(uid)).ToList(),
            NoLeidos = await _repo.ContarNoLeidosAsync(uid),
            Vista    = "entrada"
        });
    }

    // GET /Mensajes/Enviados
    public async Task<IActionResult> Enviados()
    {
        var uid = GetUserId();
        return View("Index", new BandejaViewModel
        {
            Mensajes = (await _repo.ObtenerEnviadosAsync(uid)).ToList(),
            NoLeidos = await _repo.ContarNoLeidosAsync(uid),
            Vista    = "enviados"
        });
    }

    // GET /Mensajes/Detalle/5
    public async Task<IActionResult> Detalle(int id)
    {
        var uid     = GetUserId();
        var mensaje = await _repo.ObtenerDetalleAsync(id, uid);
        if (mensaje is null) return NotFound();
        return View(mensaje);
    }

    // GET /Mensajes/Nuevo
    [HttpGet]
    public async Task<IActionResult> Nuevo(int? responderA = null)
    {
        var uid      = GetUserId();
        var usuarios = (await _repo.ListarUsuariosAsync(uid)).ToList();
        var vm = new CrearMensajeViewModel { Usuarios = usuarios };

        if (responderA.HasValue)
        {
            var original = await _repo.ObtenerDetalleAsync(responderA.Value, uid);
            if (original is not null)
            {
                vm.DestinatarioId = original.RemitenteId;
                vm.Asunto = original.Asunto.StartsWith("RE: ", StringComparison.OrdinalIgnoreCase)
                    ? original.Asunto
                    : $"RE: {original.Asunto}";
            }
        }
        return View(vm);
    }

    // POST /Mensajes/Nuevo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(CrearMensajeViewModel model)
    {
        var uid = GetUserId();
        if (!ModelState.IsValid)
        {
            model.Usuarios = (await _repo.ListarUsuariosAsync(uid)).ToList();
            return View(model);
        }
        await _repo.CrearAsync(uid, model.DestinatarioId, model.Asunto, model.Cuerpo);
        TempData["MsjExito"] = "Mensaje enviado correctamente.";
        return RedirectToAction(nameof(Enviados));
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue("UserId")
            ?? throw new InvalidOperationException("UserId claim not found"));
}
