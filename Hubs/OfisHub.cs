using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OfisServisSistemi.Hubs
{
    [Authorize]
    public class OfisHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.User != null)
            {
                // Kullanıcının kayıtlı olduğu TÜM katların ID'sini liste olarak alıyoruz.
                var katIds = Context.User.FindAll("KatId").Select(c => c.Value).ToList();

                // Kullanıcıyı sahip olduğu bütün katların anlık bildirim (soket) grubuna ekliyoruz
                foreach (var katId in katIds)
                {
                    if (!string.IsNullOrEmpty(katId))
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, katId);
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.User != null)
            {
                var katIds = Context.User.FindAll("KatId").Select(c => c.Value).ToList();

                foreach (var katId in katIds)
                {
                    if (!string.IsNullOrEmpty(katId))
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, katId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}