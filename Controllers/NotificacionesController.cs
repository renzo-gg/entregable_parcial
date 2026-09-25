using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public NotificacionesController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var usuarioId = _userManager.GetUserId(User)!;

        var avisos = await _db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.FechaRegistroUtc)
            .Select(n => new NotificacionDto(
                n.Id,
                n.Tipo,
                n.SolicitudId,
                n.Contenido,
                n.FechaRegistroUtc,
                n.FechaEventoUtc))
            .ToListAsync();

        var viewModel = new NotificacionesIndexViewModel(avisos);
        return View(viewModel);
    }
}