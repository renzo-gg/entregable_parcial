using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public interface ICacheSolicitudesService
{
    Task<IReadOnlyList<SolicitudListadoDto>?> ObtenerListadoAsync(string usuarioId);

    Task GuardarListadoAsync(string usuarioId, IReadOnlyList<SolicitudListadoDto> listado);

    Task InvalidarListadoAsync(string usuarioId);
}