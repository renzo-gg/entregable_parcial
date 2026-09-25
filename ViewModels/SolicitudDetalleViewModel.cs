using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public class SolicitudDetalleViewModel
{
    public int Id { get; set; }

    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public EstadoSolicitud Estado { get; set; }

    public string? MotivoRechazo { get; set; }

    public string ClienteEmail { get; set; } = string.Empty;

    public decimal IngresosMensuales { get; set; }

    public bool ClienteActivo { get; set; }
}