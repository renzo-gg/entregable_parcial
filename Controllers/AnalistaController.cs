using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICacheSolicitudesService _cacheSolicitudes;
    private readonly IHubContext<SolicitudesHub> _hub;

    public AnalistaController(ApplicationDbContext db, ICacheSolicitudesService cacheSolicitudes, IHubContext<SolicitudesHub> hub)
    {
        _db = db;
        _cacheSolicitudes = cacheSolicitudes;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudes = await _db.SolicitudesCreditos
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        var usuariosIds = solicitudes.Select(s => s.Cliente!.UsuarioId).Distinct().ToList();
        var emails = await _db.Users
            .Where(u => usuariosIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? "Sin correo");

        var viewModel = new AnalistaIndexViewModel
        {
            Solicitudes = solicitudes.Select(s => new AnalistaSolicitudPendienteDto(
                s.Id,
                s.MontoSolicitado,
                s.FechaSolicitud,
                emails.GetValueOrDefault(s.Cliente!.UsuarioId, "Sin correo"),
                s.Cliente!.IngresosMensuales,
                s.Cliente.IngresosMensuales * 5)).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _db.SolicitudesCreditos
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud?.Cliente is null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}).";
            return RedirectToAction(nameof(Index));
        }

        var montoMaximoAprobar = solicitud.Cliente.IngresosMensuales * 5;
        if (solicitud.MontoSolicitado > montoMaximoAprobar)
        {
            TempData["Error"] =
                $"No se puede aprobar la solicitud #{id}: el monto (S/ {solicitud.MontoSolicitado:N2}) " +
                $"supera 5 veces los ingresos mensuales del cliente (S/ {montoMaximoAprobar:N2}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _db.SaveChangesAsync();

        await _cacheSolicitudes.InvalidarListadoAsync(solicitud.Cliente.UsuarioId);
        await NotificarEstadoAsync(solicitud);

        TempData["Exito"] = $"Solicitud #{id} aprobada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Rechazar(int id)
    {
        var solicitud = await ObtenerPendienteIncluyendoClienteAsync(id);
        if (solicitud is null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada (estado actual: {solicitud.Estado}).";
            return RedirectToAction(nameof(Index));
        }

        return View(await CrearViewModelDeRechazo(solicitud));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(RechazarSolicitudViewModel model)
    {
        var solicitud = await ObtenerPendienteIncluyendoClienteAsync(model.SolicitudId);
        if (solicitud?.Cliente is null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{model.SolicitudId} ya fue procesada (estado actual: {solicitud.Estado}).";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.MontoSolicitado = solicitud.MontoSolicitado;
            model.IngresosMensuales = solicitud.Cliente!.IngresosMensuales;
            model.ClienteEmail = (await _db.Users.FindAsync(solicitud.Cliente.UsuarioId))?.Email ?? "Sin correo";
            return View(model);
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = model.MotivoRechazo!.Trim();
        await _db.SaveChangesAsync();

        await _cacheSolicitudes.InvalidarListadoAsync(solicitud.Cliente.UsuarioId);
        await NotificarEstadoAsync(solicitud);

        TempData["Exito"] = $"Solicitud #{solicitud.Id} rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SolicitudCredito?> ObtenerPendienteIncluyendoClienteAsync(int id)
        => await _db.SolicitudesCreditos
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

    private async Task NotificarEstadoAsync(SolicitudCredito solicitud)
    {
        if (solicitud.Cliente is null)
        {
            return;
        }

        await _hub.Clients.User(solicitud.Cliente.UsuarioId)
            .SendAsync(
                "SolicitudEstadoActualizado",
                solicitud.Id,
                solicitud.Estado.ToString(),
                solicitud.MotivoRechazo);
    }

    private async Task<RechazarSolicitudViewModel> CrearViewModelDeRechazo(SolicitudCredito solicitud)
    {
        var email = (await _db.Users.FindAsync(solicitud.Cliente!.UsuarioId))?.Email ?? "Sin correo";
        return new RechazarSolicitudViewModel
        {
            SolicitudId = solicitud.Id,
            MontoSolicitado = solicitud.MontoSolicitado,
            IngresosMensuales = solicitud.Cliente.IngresosMensuales,
            ClienteEmail = email
        };
    }
}