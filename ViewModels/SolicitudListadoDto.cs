using PlataformaCreditos.Models;

namespace PlataformaCreditos.ViewModels;

public sealed record SolicitudListadoDto(int Id, decimal MontoSolicitado, DateTime FechaSolicitud, EstadoSolicitud Estado);