using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DChemist.Utils
{
    /// <summary>
    /// An ObservableCollection that supports bulk replacement with a single UI notification.
    /// Instead of firing CollectionChanged N times (once per Add), this fires a single Reset event.
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification;

        /// <summary>
        /// Replaces all items in the collection with the provided items,
        /// firing only a single CollectionChanged (Reset) notification at the end.
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            _suppressNotification = true;
            try
            {
                Items.Clear();
                foreach (var item in items)
                    Items.Add(item);
            }
            finally
            {
                _suppressNotification = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }
    }
}
