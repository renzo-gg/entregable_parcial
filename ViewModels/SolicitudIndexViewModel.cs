using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public class SolicitudIndexViewModel
{
    public IReadOnlyList<SolicitudListadoDto> Solicitudes { get; set; } = Array.Empty<SolicitudListadoDto>();

    public EstadoSolicitud? Estado { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto minimo no puede ser negativo.")]
    public decimal? MontoMin { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto maximo no puede ser negativo.")]
    public decimal? MontoMax { get; set; }

    [DataType(DataType.Date)]
    public DateTime? FechaDesde { get; set; }

    [DataType(DataType.Date)]
    public DateTime? FechaHasta { get; set; }
}