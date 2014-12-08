using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicStarViewer {
    public class ObservableSortedCollection<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged {
        //-------------------------------------------------------------------------------
        #region SimpleMonitor
        //-------------------------------------------------------------------------------
        sealed class SimpleMonitor : IDisposable {
            private int _busyCount;

            public SimpleMonitor() {
            }

            public void Enter() {
                _busyCount++;
            }

            public void Dispose() {
                _busyCount--;
            }

            public bool Busy {
                get { return _busyCount > 0; }
            }
        }

        private SimpleMonitor _monitor = new SimpleMonitor();
        //-------------------------------------------------------------------------------
        #endregion (SimpleMonitor)

        private List<T> _defaultSortList = new List<T>();

        private IComparer<T> _comparer = null;
        public IComparer<T> Comparer {
            get { return _comparer; }
            set {
                if (_comparer != value) {
                    _comparer = value;
                    var items = _defaultSortList.ToArray();

                    this.Clear();
                    foreach (var item in items) { this.Add(item); }
                }
            }
        }

        public ObservableSortedCollection() {
        }

        public ObservableSortedCollection(IEnumerable<T> collection) {
            if (collection == null)
                throw new ArgumentNullException("collection");

            _defaultSortList.AddRange(collection);

            foreach (var item in _defaultSortList)
                Add(item);
        }

        public ObservableSortedCollection(List<T> list)
            : base(list != null ? new List<T>(list) : null) {
                if (list != null) { _defaultSortList.AddRange(list); }
        }

        public virtual event NotifyCollectionChangedEventHandler CollectionChanged;
        protected virtual event PropertyChangedEventHandler PropertyChanged;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
            add { this.PropertyChanged += value; }
            remove { this.PropertyChanged -= value; }
        }

        protected IDisposable BlockReentrancy() {
            _monitor.Enter();
            return _monitor;
        }

        protected void CheckReentrancy() {
            NotifyCollectionChangedEventHandler eh = CollectionChanged;

            // Only have a problem if we have more than one event listener.
            if (_monitor.Busy && eh != null && eh.GetInvocationList().Length > 1)
                throw new InvalidOperationException("Cannot modify the collection while reentrancy is blocked.");
        }

        protected override void ClearItems() {
            CheckReentrancy();

            base.ClearItems();
            _defaultSortList.Clear();

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }

        protected override void InsertItem(int index, T item) {
            if (index == this.Count) {
                int insertIndex = (_comparer == null) ? index : this.BinarySearch(item);
                
                base.InsertItem(insertIndex, item);
                _defaultSortList.Add(item);

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, insertIndex));
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            }
            else {
                throw new NotSupportedException();
            }
            /*CheckReentrancy();

            base.InsertItem(index, item);*/
        }

        private int BinarySearch(T item) {
            if (_comparer == null) {
                throw new InvalidOperationException("No comparer");
            }
            int lower = 0;
            int upper = this.Count - 1;

            while (lower <= upper) {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = _comparer.Compare(item, Items[middle]);
                if (comparisonResult < 0) {
                    upper = middle - 1;
                }
                else if (comparisonResult > 0) {
                    lower = middle + 1;
                }
                else {
                    return middle;
                }
            }

            return lower;
        }

        public void Move(int oldIndex, int newIndex) {
            throw new NotSupportedException();
            //MoveItem(oldIndex, newIndex);
        }

        protected virtual void MoveItem(int oldIndex, int newIndex) {
            throw new NotSupportedException();
            /*CheckReentrancy();

            T item = Items[oldIndex];
            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, item);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));*/
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            NotifyCollectionChangedEventHandler eh = CollectionChanged;

            if (eh != null) {
                // Make sure that the invocation is done before the collection changes,
                // Otherwise there's a chance of data corruption.
                using (BlockReentrancy()) {
                    eh(this, e);
                }
            }
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) {
            PropertyChangedEventHandler eh = PropertyChanged;

            if (eh != null)
                eh(this, e);
        }

        protected override void RemoveItem(int index) {
            CheckReentrancy();

            T item = Items[index];

            _defaultSortList.Remove(item);
            base.RemoveItem(index);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }

        protected override void SetItem(int index, T item) {
            throw new NotSupportedException();
            /*CheckReentrancy();

            T oldItem = Items[index];

            base.SetItem(index, item);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));*/
        }

        public void Refresh(int index) {
            T item = Items[index];
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, item, index));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }
    }
}
