using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicStarViewer
{
    class Bookmarks : INotifyPropertyChanged
    {
        private static string BOOKMARK_DEFAULT_FILENAME_FORMAT = "bookmark{0}.xml";
        private int _no = 0;
        private bool _initialized = false;
        public ObservableCollection<BookmarkData> Data { get; private set; }

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public Bookmarks()
        {
            Data = new ObservableCollection<BookmarkData>();
        }
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region -[static]GetFileName ファイル名
        //-------------------------------------------------------------------------------
        //
        public static string GetFileName(int no)
        {
            return string.Format(BOOKMARK_DEFAULT_FILENAME_FORMAT, no);
        }
        #endregion (GetFileName)

        //-------------------------------------------------------------------------------
        #region +[async]Initialize 初期化
        //-------------------------------------------------------------------------------
        //
        public async Task Initialize(int no)
        {
            _no = no;

            var res = await Util.RestoreFromXmlFile<ObservableCollection<BookmarkData>>(Bookmarks.GetFileName(no));
            if (res.Item1) {
                // successed
                Data = res.Item2;
            }

            _initialized = true;
            OnPropertyChanged(new PropertyChangedEventArgs("Data"));
        }
        #endregion (Initialize)

        //-------------------------------------------------------------------------------
        #region +Add 追加
        //-------------------------------------------------------------------------------
        /// <summary>
        /// ブックマークに追加
        /// </summary>
        /// <param name="name">ブックマーク名</param>
        /// <param name="day">日付インデックス(0開始)</param>
        /// <param name="index">その日付の中での要素位置</param>
        /// <returns>追加成功でtrue</returns>
        public async Task<bool> Add(string name, int day, int index, string area, char block)
        {
            if (!_initialized) { return false; }
            if (Data.Any(bd => bd.Name == name)) { return false; }
            Data.Add(new BookmarkData() { Name = name, Day = day, Index = index, Area = area, Block = block});

            // 保存
            await Util.SaveToXmlFile<ObservableCollection<BookmarkData>>(Data, Bookmarks.GetFileName(_no));
            OnPropertyChanged(new PropertyChangedEventArgs("Data"));

            return true;
        }
        #endregion (Add)
        //-------------------------------------------------------------------------------
        #region +Remove 削除
        //-------------------------------------------------------------------------------
        /// <summary>
        /// ブックマークから削除
        /// </summary>
        /// <param name="name">ブックマーク名</param>
        /// <returns>削除成功時true</returns>
        public async Task<bool> Remove(string name)
        {
            if (!_initialized) { return false; }
            var first = Data.FirstOrDefault(bd => bd.Name == name);
            if (first == null) { return false; }
            Data.Remove(first);

            // 保存
            await Util.SaveToXmlFile<ObservableCollection<BookmarkData>>(Data, Bookmarks.GetFileName(_no));
            OnPropertyChanged(new PropertyChangedEventArgs("Data"));

            return true;
        }
        #endregion (Remove)

        //-------------------------------------------------------------------------------
        #region #OnPropertyChanged
        //-------------------------------------------------------------------------------
        //
        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion (#OnPropertyChanged)
    }

    public class BookmarkData
    {
        public string Name { get; set; }
        public int Day { get; set; }
        public int DayBase1 { get { return Day + 1; } }
        public int Index { get; set; }
        public string Area { get; set; }
        public char Block { get; set; }
    }
}