using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace DChemist.Services
{
    /// <summary>
    /// A singleton pub/sub event bus for inventory changes.
    /// Any service can Publish() after a DB change;
    /// any ViewModel subscribes to InventoryChanged to auto-refresh.
    ///
    /// Future: Replace the in-process event with a SignalR/WebSocket hub
    ///         for multi-PC synchronization — ViewModels need zero changes.
    /// </summary>
    public class InventoryEventBus
    {
        /// <summary>
        /// Raised whenever inventory data changes in the database.
        /// Subscribers receive the type of change that occurred.
        /// </summary>
        public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;

        /// <summary>
        /// Publishes an inventory change event to all subscribers.
        /// Safe to call from any thread.
        /// </summary>
        public void Publish(InventoryChangeType changeType = InventoryChangeType.General)
        {
            InventoryChanged?.Invoke(this, new InventoryChangedEventArgs(changeType));
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

    /// <summary>
    /// Describes what kind of inventory change occurred.
    /// Allows subscribers to selectively react.
    /// </summary>
    public enum InventoryChangeType
    {
        General,
        MedicineAdded,
        MedicineUpdated,
        MedicineDeleted,
        StockDeducted,   // After a sale is completed
        StockAdjusted,   // After a void or manual adjustment
    }
}
