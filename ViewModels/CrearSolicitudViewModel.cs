using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.ViewModels;

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [DataType(DataType.Currency)]
    public decimal MontoSolicitado { get; set; }

    public decimal IngresosMensuales { get; set; }

    public string? BloqueoMensaje { get; set; }
}