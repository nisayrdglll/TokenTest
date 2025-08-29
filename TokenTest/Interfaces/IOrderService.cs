using TokenManager.ClassLibrary.Models;

namespace TokenManager.ClassLibrary.Interfaces
{
    public interface IOrderService
    { 
        Task<List<OrderItem>> GetOrderListAsync();

        void StartPeriodicFetch(int intervalMinutes = 5);

        void StopPeriodicFetch();

        event Action<List<OrderItem>>? OrderListUpdated;

        event Action<Exception>? ErrorOccurred;

        bool IsRunning { get; }
    }
}