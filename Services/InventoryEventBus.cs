using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using DChemist.Utils;

namespace DChemist.Services
{
    public class InventoryEventBus
    {
        public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;
        public void Publish(InventoryChangeType changeType = InventoryChangeType.General)
        {
            var handlers = InventoryChanged;
            if (handlers == null) return;

            var args = new InventoryChangedEventArgs(changeType);
            foreach (EventHandler<InventoryChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryEventBus] Subscriber error: {ex.Message}");
                    AppLogger.LogError($"InventoryEventBus subscriber error", ex);
                }
            }
        }
    }

    public class InventoryChangedEventArgs : EventArgs
    {
        public InventoryChangeType ChangeType { get; }
        public DateTime OccurredAt { get; } = DateTime.Now;

        public InventoryChangedEventArgs(InventoryChangeType changeType)
        {
            ChangeType = changeType;
        }
    }

    public enum InventoryChangeType
    {
        General,
        MedicineAdded,
        MedicineUpdated,
        MedicineDeleted,
        StockDeducted,   
        StockAdjusted,   
    }
}
