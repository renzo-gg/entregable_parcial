using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(SolicitudIndexViewModel model)
    {
        var usuarioId = _userManager.GetUserId(User)!;

        IQueryable<SolicitudCredito> query = _db.SolicitudesCreditos
            .Include(s => s.Cliente)
            .Where(s => s.Cliente!.UsuarioId == usuarioId);

        ValidarFiltros(model);

        if (model.Estado.HasValue)
        {
            query = query.Where(s => s.Estado == model.Estado.Value);
        }

        if (ModelState.IsValid)
        {
            if (model.MontoMin.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= model.MontoMin.Value);
            }

            if (model.MontoMax.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= model.MontoMax.Value);
            }

            if (model.FechaDesde.HasValue)
            {
                var desde = model.FechaDesde.Value;
                query = query.Where(s => s.FechaSolicitud >= desde);
            }

            if (model.FechaHasta.HasValue)
            {
                var hasta = model.FechaHasta.Value.Date.AddDays(1);
                query = query.Where(s => s.FechaSolicitud < hasta);
            }
        }

        model.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ThenByDescending(s => s.Id)
            .ToListAsync();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var usuarioId = _userManager.GetUserId(User)!;

        var solicitud = await _db.SolicitudesCreditos
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.Cliente!.UsuarioId == usuarioId);

        if (solicitud is null || solicitud.Cliente is null)
        {
            return NotFound();
        }

        var clienteEmail = (await _db.Users.FindAsync(solicitud.Cliente.UsuarioId))?.Email ?? "Sin correo";

        var viewModel = new SolicitudDetalleViewModel
        {
            Id = solicitud.Id,
            MontoSolicitado = solicitud.MontoSolicitado,
            FechaSolicitud = solicitud.FechaSolicitud,
            Estado = solicitud.Estado,
            MotivoRechazo = solicitud.MotivoRechazo,
            ClienteEmail = clienteEmail,
            IngresosMensuales = solicitud.Cliente.IngresosMensuales,
            ClienteActivo = solicitud.Cliente.Activo
        };

        return View(viewModel);
    }

    private void ValidarFiltros(SolicitudIndexViewModel model)
    {
        if (model.MontoMin.HasValue && model.MontoMax.HasValue && model.MontoMin > model.MontoMax)
        {
            ModelState.AddModelError(nameof(model.MontoMax), "El monto maximo no puede ser menor al monto minimo.");
        }

        if (model.FechaDesde.HasValue && model.FechaHasta.HasValue && model.FechaDesde > model.FechaHasta)
        {
            ModelState.AddModelError(nameof(model.FechaHasta), "La fecha fin no puede ser anterior a la fecha inicio.");
        }
    }
}