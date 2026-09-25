namespace PlataformaCreditos.Mensajes;

public sealed record SolicitudRegistradaEvent(
    string Tipo,
    Guid MessageId,
    int SolicitudId,
    string UsuarioId,
    DateTime FechaEventoUtc);