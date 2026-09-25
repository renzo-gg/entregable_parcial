using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public class CacheSolicitudesService(IDistributedCache cache, ILogger<CacheSolicitudesService> logger)
    : ICacheSolicitudesService
{
    private static readonly TimeSpan TtlDelListado = TimeSpan.FromSeconds(60);
    private const string PrefijoClave = "solicitudes:v1:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<SolicitudListadoDto>?> ObtenerListadoAsync(string usuarioId)
    {
        var bytes = await cache.GetAsync(Clave(usuarioId));
        if (bytes is null)
        {
            logger.LogInformation("Cache MISS listado solicitudes para {UsuarioId}", usuarioId);
            return null;
        }

        logger.LogInformation("Cache HIT listado solicitudes para {UsuarioId}", usuarioId);
        return JsonSerializer.Deserialize<List<SolicitudListadoDto>>(bytes, JsonOptions);
    }

    public async Task GuardarListadoAsync(string usuarioId, IReadOnlyList<SolicitudListadoDto> listado)
    {
        await cache.SetAsync(
            Clave(usuarioId),
            JsonSerializer.SerializeToUtf8Bytes(listado, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TtlDelListado });
    }

    public Task InvalidarListadoAsync(string usuarioId) => cache.RemoveAsync(Clave(usuarioId));

    private static string Clave(string usuarioId) => $"{PrefijoClave}{usuarioId}";
}