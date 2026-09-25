using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public class AnalistaIndexViewModel
{
    public IReadOnlyList<AnalistaSolicitudPendienteDto> Solicitudes { get; set; } = Array.Empty<AnalistaSolicitudPendienteDto>();
}

public sealed record AnalistaSolicitudPendienteDto(
    int Id,
    decimal MontoSolicitado,
    DateTime FechaSolicitud,
    string ClienteEmail,
    decimal IngresosMensuales,
    decimal MontoMaximoAprobar);

public class RechazarSolicitudViewModel
{
    public int SolicitudId { get; set; }

    [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener al menos 5 caracteres.")]
    public string? MotivoRechazo { get; set; }

    public decimal MontoSolicitado { get; set; }

    public decimal IngresosMensuales { get; set; }

    public string ClienteEmail { get; set; } = string.Empty;
}