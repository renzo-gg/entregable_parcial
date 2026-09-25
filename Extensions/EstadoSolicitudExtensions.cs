using PlataformaCreditos.Models;

namespace PlataformaCreditos.Extensions;

public static class EstadoSolicitudExtensions
{
    public static string BadgeCssClass(this EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Pendiente => "bg-warning text-dark",
        EstadoSolicitud.Aprobado => "bg-success",
        EstadoSolicitud.Rechazado => "bg-danger",
        _ => "bg-secondary"
    };
}