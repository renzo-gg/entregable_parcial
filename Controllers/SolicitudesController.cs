using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ICacheSolicitudesService _cacheSolicitudes;
    private readonly ISesionSolicitudService _sesionSolicitud;
    private readonly IProductorNotificacionSolicitud _productorNotificacionSolicitud;

    public SolicitudesController(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        ICacheSolicitudesService cacheSolicitudes,
        ISesionSolicitudService sesionSolicitud,
        IProductorNotificacionSolicitud productorNotificacionSolicitud)
    {
        _db = db;
        _userManager = userManager;
        _cacheSolicitudes = cacheSolicitudes;
        _sesionSolicitud = sesionSolicitud;
        _productorNotificacionSolicitud = productorNotificacionSolicitud;
    }

    [HttpGet]
    public async Task<IActionResult> Index(SolicitudIndexViewModel model)
    {
        var usuarioId = _userManager.GetUserId(User)!;

        var listado = await _cacheSolicitudes.ObtenerListadoAsync(usuarioId);
        if (listado is null)
        {
            listado = await _db.SolicitudesCreditos
                .Where(s => s.Cliente!.UsuarioId == usuarioId)
                .OrderByDescending(s => s.FechaSolicitud)
                .ThenByDescending(s => s.Id)
                .Select(s => new SolicitudListadoDto(s.Id, s.MontoSolicitado, s.FechaSolicitud, s.Estado))
                .ToListAsync();

            await _cacheSolicitudes.GuardarListadoAsync(usuarioId, listado);
        }

        ValidarFiltros(model);

        IEnumerable<SolicitudListadoDto> resultados = listado;

        if (model.Estado.HasValue)
        {
            resultados = resultados.Where(s => s.Estado == model.Estado.Value);
        }

        if (ModelState.IsValid)
        {
            if (model.MontoMin.HasValue)
            {
                resultados = resultados.Where(s => s.MontoSolicitado >= model.MontoMin.Value);
            }

            if (model.MontoMax.HasValue)
            {
                resultados = resultados.Where(s => s.MontoSolicitado <= model.MontoMax.Value);
            }

            if (model.FechaDesde.HasValue)
            {
                var desde = model.FechaDesde.Value;
                resultados = resultados.Where(s => s.FechaSolicitud >= desde);
            }

            if (model.FechaHasta.HasValue)
            {
                var hasta = model.FechaHasta.Value.Date.AddDays(1);
                resultados = resultados.Where(s => s.FechaSolicitud < hasta);
            }
        }

        model.Solicitudes = resultados.ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var usuarioId = _userManager.GetUserId(User)!;

        var model = new CrearSolicitudViewModel();
        await CargarContextoClienteAsync(model, usuarioId);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearSolicitudViewModel model)
    {
        var usuarioId = _userManager.GetUserId(User)!;

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente is null)
        {
            model.BloqueoMensaje = "Tu usuario no tiene un perfil de cliente registrado en la plataforma.";
            ModelState.AddModelError(string.Empty, model.BloqueoMensaje);
        }
        else
        {
            model.IngresosMensuales = cliente.IngresosMensuales;

            if (!cliente.Activo)
            {
                model.BloqueoMensaje = "Tu perfil de cliente se encuentra inactivo, no puedes solicitar creditos.";
                ModelState.AddModelError(string.Empty, model.BloqueoMensaje);
            }
            else
            {
                var tienePendiente = await _db.SolicitudesCreditos.AnyAsync(s =>
                    s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

                if (tienePendiente)
                {
                    ModelState.AddModelError(nameof(model.MontoSolicitado),
                        "Ya tienes una solicitud en estado Pendiente. Solo se permite una a la vez.");
                }

                var montoMaximo = cliente.IngresosMensuales * 10;
                if (model.MontoSolicitado > montoMaximo)
                {
                    ModelState.AddModelError(nameof(model.MontoSolicitado),
                        $"El monto solicitado no puede superar 10 veces tus ingresos mensuales (maximo S/ {montoMaximo:N2}).");
                }
            }
        }

        if (ModelState.IsValid)
        {
            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente!.Id,
                MontoSolicitado = model.MontoSolicitado,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            };

            _db.SolicitudesCreditos.Add(solicitud);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "No se pudo registrar la solicitud porque ya tienes una en estado Pendiente. Reintenta en unos segundos.");
                return View(model);
            }

            TempData["Exito"] = "Solicitud registrada correctamente en estado Pendiente.";
            await _cacheSolicitudes.InvalidarListadoAsync(usuarioId);
            await _productorNotificacionSolicitud.PublicarSolicitudRegistradaAsync(solicitud.Id, usuarioId);
            return View(model);
        }

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

        _sesionSolicitud.GuardarUltimaSolicitud(solicitud.Id, solicitud.MontoSolicitado);

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

    private async Task<bool> CargarContextoClienteAsync(CrearSolicitudViewModel model, string usuarioId)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente is null)
        {
            model.BloqueoMensaje = "Tu usuario no tiene un perfil de cliente registrado en la plataforma.";
            return false;
        }

        model.IngresosMensuales = cliente.IngresosMensuales;

        if (!cliente.Activo)
        {
            model.BloqueoMensaje = "Tu perfil de cliente se encuentra inactivo, no puedes solicitar creditos.";
            return false;
        }

        return true;
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