using Microsoft.AspNetCore.SignalR;

namespace ClassmateQRapi.Hubs
{
    public class NotificationHub : Hub
    {
        // Sinh viên join vào phòng của lớp
        public async Task JoinClass(string classId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"class_{classId}"
            );
        }

        public async Task LeaveClass(string classId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"class_{classId}"
            );
        }
    }
}
