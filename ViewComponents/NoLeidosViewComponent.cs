// ============================================================
// ViewComponents/NoLeidosViewComponent.cs
// ViewComponent que muestra el badge de mensajes no leídos
// en el topbar del _Layout autenticado.
// ============================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using LoginAppCore.Data;

namespace LoginAppCore.ViewComponents;

public class NoLeidosViewComponent : ViewComponent
{
    private readonly IMensajeRepository _repo;

    public NoLeidosViewComponent(IMensajeRepository repo) => _repo = repo;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var claim = HttpContext.User.FindFirstValue("UserId");
        if (claim is null || !int.TryParse(claim, out int uid))
            return Content(string.Empty);

        var count = await _repo.ContarNoLeidosAsync(uid);
        return View(count);
    }
}
