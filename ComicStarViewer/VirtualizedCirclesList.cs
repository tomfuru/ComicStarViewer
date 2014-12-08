using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicStarViewer {
    public class VirtualizedCirclesList : IList, INotifyPropertyChanged, INotifyCollectionChanged {
        const int NUM_PER_BLOCK = 100;
        const int REMOVE_DIST_THRES = 5;

        private CatalogData _catalogData;
        private CheckList _checkList;
        private Dictionary<int, ThumbDispData[]> _indexToBlockDic = new Dictionary<int, ThumbDispData[]>();
        private int _count = -1;
        private HashSet<int> _gettingBlock = new HashSet<int>();

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public VirtualizedCirclesList(CatalogData catalogData, CheckList checklist) {
            _catalogData = catalogData;
            _checkList = checklist;
        }
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region -GetBlockAsync ブロック分のサークル取得
        //-------------------------------------------------------------------------------
        //
        private async void GetAndSetBlockAsync(int blockIndex) {
            var res = await _catalogData.GetCirclesFromIndexAsync(blockIndex * NUM_PER_BLOCK, NUM_PER_BLOCK);
            var data = res.Select(cc => {
                var tdd = new ThumbDispData(cc.SpaceStr, cc.updateId, cc, _checkList);
                tdd.GetImageAsync();
                return tdd;
            }).ToArray();

            lock (_indexToBlockDic) {
                if (!_indexToBlockDic.ContainsKey(blockIndex)) {
                    _indexToBlockDic.Add(blockIndex, data);
                }
            }

            lock (_gettingBlock) {
                _gettingBlock.Remove(blockIndex);
            }

            for (int i = 0; i < data.Length; i++) {
                OnNofityCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, data[i], null, blockIndex * NUM_PER_BLOCK + i));
            }
            
            RemoveFarData(blockIndex);
        }
        #endregion (GetBlockAsync)
        //-------------------------------------------------------------------------------
		#region +GetBlockByIndex インデックス指定でブロック分サークル取得
		//-------------------------------------------------------------------------------
		//
		public void GetBlockByIndex(int index)
		{
            int blockIndex = index / NUM_PER_BLOCK;

            lock (_indexToBlockDic) {
                if (_indexToBlockDic.ContainsKey(blockIndex)) { return; }
            }

			var res = _catalogData.GetCirclesFromIndex(blockIndex * NUM_PER_BLOCK, NUM_PER_BLOCK);
            var data = res.Select(cc => {
                var tdd = new ThumbDispData(cc.SpaceStr, cc.updateId, cc, _checkList);
                tdd.GetImageAsync();
                return tdd;
            }).ToArray();

            lock (_indexToBlockDic) {
                _indexToBlockDic.Add(blockIndex, data);
            }
		}
		#endregion (GetBlockByIndex)
        //-------------------------------------------------------------------------------
        #region -RemoveFarData 遠いデータ削除
        //-------------------------------------------------------------------------------
        //
        private void RemoveFarData(int currBlockIndex) {
            List<int> removeKeys = new List<int>();
            var keys = _indexToBlockDic.Keys;
            foreach (var b in keys) {
                if (Math.Abs(b - currBlockIndex) > REMOVE_DIST_THRES) {
                    removeKeys.Add(b);
                }
            }

            foreach (var b in removeKeys) {
                lock (_indexToBlockDic) {
                    _indexToBlockDic.Remove(b);
                }
                GC.Collect();
            }
        }
        #endregion (RemoveFarData)

        //-------------------------------------------------------------------------------
        #region -GetValueFromIndex インデックスから値取得
        //-------------------------------------------------------------------------------
        //
        private ThumbDispData GetValueFromIndex(int index) {
            int blockIndex = index / NUM_PER_BLOCK;
            int indexInBlock = index % NUM_PER_BLOCK;

            bool getting;
            lock (_gettingBlock) {
                getting = _gettingBlock.Contains(blockIndex);
            }

            if (!_indexToBlockDic.ContainsKey(blockIndex)) {
                if (!getting) {
                    lock (_gettingBlock) {
                        _gettingBlock.Add(blockIndex);
                    }
                    GetAndSetBlockAsync(blockIndex);
                    return null;
                }
                else {
                    return null;
                }
            }

            /*int nextBlockIndex = blockIndex + 1;
            if (!_indexToBlockDic.ContainsKey(nextBlockIndex)) {
                bool getting_next;
                lock (_gettingBlock) {
                    getting_next = _gettingBlock.Contains(nextBlockIndex);
                }

                if (!getting_next) {
                    lock (_gettingBlock) {
                        _gettingBlock.Add(nextBlockIndex);
                    }
                    GetAndSetBlockAsync(nextBlockIndex);
                }
            }*/

            lock (_indexToBlockDic) {
                return _indexToBlockDic[blockIndex][indexInBlock];
            }
        }
        #endregion (GetValueFromIndex)

        public int Count {
            get {
                if (_count < 0) {
                    var count = _catalogData.GetCirclesCount();

                    _count = count;
                    return _count;
                }
                return _count;
            }
        }


        public object this[int index] {
            get {
                return GetValueFromIndex(index);
            }
            set {
                throw new NotImplementedException();
            }
        }

        public int IndexOf(object value) {
            var item = value as ThumbDispData;
            if (item == null) { return -1; }

            lock (_indexToBlockDic) {
                foreach (var blockId in _indexToBlockDic.Keys) {
                    int index = Array.IndexOf(_indexToBlockDic[blockId], item);
                    if (index >= 0) { return blockId * NUM_PER_BLOCK + index; }
                }
            }
            return -1;
        }

        //-------------------------------------------------------------------------------
        #region #OnNofityCollectionChanged
        //-------------------------------------------------------------------------------
        protected void OnNofityCollectionChanged(NotifyCollectionChangedEventArgs args) {
            if (CollectionChanged != null) {
                CollectionChanged(this, args);
            }
        }
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        //-------------------------------------------------------------------------------
        #endregion (#OnNofityCollectionChanged)

        //-------------------------------------------------------------------------------
        #region #OnPropertyChanged
        //-------------------------------------------------------------------------------
        //
        protected void OnPropertyChanged(PropertyChangedEventArgs arg) {
            if (PropertyChanged != null) {
                PropertyChanged(this, arg);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        //-------------------------------------------------------------------------------
        #endregion (#OnPropertyChanged)

        //-------------------------------------------------------------------------------
        #region Need not implement
        //-------------------------------------------------------------------------------
        public int Add(object value) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(object value) {
            throw new NotImplementedException();
        }

        public void Insert(int index, object value) {
            throw new NotImplementedException();
        }

        public bool IsFixedSize {
            get { throw new NotImplementedException(); }
        }

        public bool IsReadOnly {
            get { throw new NotImplementedException(); }
        }

        public void Remove(object value) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public bool IsSynchronized {
            get { throw new NotImplementedException(); }
        }

        public object SyncRoot {
            get { throw new NotImplementedException(); }
        }

        public IEnumerator GetEnumerator() {
            throw new NotImplementedException();
        }
        //-------------------------------------------------------------------------------
        #endregion (Need not implement)

    }
}
