namespace PlataformaCreditos.ViewModels;

public sealed record NotificacionDto(
    int Id,
    string Tipo,
    int SolicitudId,
    string Contenido,
    DateTime FechaRegistroUtc,
    DateTime FechaEventoUtc);

public sealed record NotificacionesIndexViewModel(IReadOnlyList<NotificacionDto> Notificaciones);