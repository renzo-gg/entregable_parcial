using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Context.UserIdentifier);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.UserIdentifier is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Context.UserIdentifier);
        }

        await base.OnDisconnectedAsync(exception);
    }
}