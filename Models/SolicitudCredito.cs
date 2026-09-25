using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public EstadoSolicitud Estado { get; set; }

    public string? MotivoRechazo { get; set; }
}