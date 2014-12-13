using ComicStarViewer.Common;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicStarViewer {
    //-------------------------------------------------------------------------------
    #region (class)ModelView
    //-------------------------------------------------------------------------------
    class MyModelView : INotifyPropertyChanged {
        public CatalogData CatalogData { get; private set; }
        public WebCatalogData WebCatalogData { get; private set; }
        private VirtualizedCirclesList _vlist = null;

        //-------------------------------------------------------------------------------
        #region 表示用プロパティ
        //-------------------------------------------------------------------------------

        public CheckList CheckList { get; private set; }
        public bool IsValidCheckList { get { return CheckList != null; } }

        public Func<IEnumerable<CheckFileCircleInfo>, IOrderedEnumerable<CheckFileCircleInfo>> CircleList_Sort { get; private set; }
        public Func<IEnumerable<CheckFileUnknownCircleInfo>, IOrderedEnumerable<CheckFileUnknownCircleInfo>> CircleList_Unknown_Sort { get; private set; }
        private readonly CheckListSortingType[] _sortTypes = new CheckListSortingType[] { CheckListSortingType.None, CheckListSortingType.Color_Asc, CheckListSortingType.Color_Desc, CheckListSortingType.Place_Asc, CheckListSortingType.Place_Desc };
        public CheckListSortingType[] SortTypes { get { return _sortTypes; } }
        public CheckListSortingType SelectedSortType { get; set; }

        public IEnumerable<CheckFileCircleInfo> CircleList_Day1_Sorted { get { return (CheckList == null) ? null : ((CircleList_Sort == null) ? CheckList.CircleList_Day1 : CircleList_Sort(CheckList.CircleList_Day1).AsEnumerable()); } }
        public IEnumerable<CheckFileCircleInfo> CircleList_Day2_Sorted { get { return (CheckList == null) ? null : ((CircleList_Sort == null) ? CheckList.CircleList_Day2 : CircleList_Sort(CheckList.CircleList_Day2).AsEnumerable()); } }
        public IEnumerable<CheckFileCircleInfo> CircleList_Day3_Sorted { get { return (CheckList == null) ? null : ((CircleList_Sort == null) ? CheckList.CircleList_Day3 : CircleList_Sort(CheckList.CircleList_Day3).AsEnumerable()); } }
        public IEnumerable<CheckFileUnknownCircleInfo> CircleList_Unknown_Sorted { get { return (CheckList == null) ? null : ((CircleList_Unknown_Sort == null) ? CheckList.CircleList_Unknown : CircleList_Unknown_Sort(CheckList.CircleList_Unknown).AsEnumerable()); } }

        public DayOfWeek[] DayOfWeek_Data { get; private set; }
        public string[] Area_Data { get; private set; }
        public char[] Block_Data { get; private set; }
        public string[] Genre_Data { get; private set; }

        public VirtualizedCirclesList Items { get; private set; }

        public CircleDetailDispData Detail { get; private set; }

        public BitmapImage MapImage { get; private set; }

        public int MapCircleWidth { get; private set; }
        public int MapCircleHeight { get; private set; }
        public Thickness MapSelectedMargin { get; private set; }
        public BitmapImage MapSelectedCircleCut { get; private set; }

        public CheckFileCircleInfo SelectedCheckListItem { get; private set; }
        public CheckFileUnknownCircleInfo SelectedCheckListUnknownItem { get; private set; }
        public string SelectedCheckListItemMemo { get; set; }
        private string _prev_memo = null;
        public DateTime? SelectedCheckListItemTime_startLine { get; private set; }
        private DateTime? _prevDateTime_startline = null;
        public DateTime? SelectedCheckListItemTime_bought { get; private set; }
        private DateTime? _prevDateTime_bought = null;
        //-------------------------------------------------------------------------------
        #endregion (表示用プロパティ)

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public MyModelView() {
            SelectedSortType = CheckListSortingType.None;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedSortType"));
        }
        //-------------------------------------------------------------------------------
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region +[async]Initialize 初期化(CatalogData, CheckList等)
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> Initialize()
        {
            WebCatalogData = new WebCatalogData();
            // カタログオープン
            CatalogData = await CatalogData.OpenCatalogData();
            
            if (CatalogData == null) { return false; }

            // チェックリストを空で初期化
            var info = await CatalogData.GetComiketInfo();
            CheckList = new CheckList(info.comiketNo);
            CheckList.PropertyChanged += CheckList_PropertyChanged;
            /*
            // 既存ファイルがある場合は開く
            if (await CheckList.CheckExistCheckList()) {
                await this.OpenCheckList(await CheckList.OpenExistCheckList());
            }
            */

            OnPropertyChanged(new PropertyChangedEventArgs("CheckList"));

            // 仮想リスト設定
            _vlist = new VirtualizedCirclesList(CatalogData, CheckList);
            Items = _vlist;
            OnPropertyChanged(new PropertyChangedEventArgs("Items"));

            // Combobox 初期化
            DayOfWeek_Data = await CatalogData.GetDays();
            OnPropertyChanged(new PropertyChangedEventArgs("DayOfWeek_Data"));
            Area_Data = await CatalogData.GetAreas();
            OnPropertyChanged(new PropertyChangedEventArgs("Area_Data"));
            Genre_Data = await CatalogData.GetGenres();
            OnPropertyChanged(new PropertyChangedEventArgs("Genre_Data"));

            return true;
        }
        #endregion (Initialize)

        //-------------------------------------------------------------------------------
        #region +[async]SetCircleDetail サークル詳細表示
        //-------------------------------------------------------------------------------
        //
        public async Task SetCircleDetail(ComiketCircle circle)
        {
            Detail = await CircleDetailDispData.ConvertToCircleDetailDispData(circle, CatalogData.GetBlockChr, CatalogData.GetGenreStringById);
            OnPropertyChanged(new PropertyChangedEventArgs("Detail"));

            var info = await CatalogData.GetComiketInfo();
            var pos = await CatalogData.GetMapPlace(circle.blockId, circle.spaceNo);
            var bigarea = await CatalogData.GetAreaChar(circle.blockId);

            var rect = ComiketUtil.GetCircleRect(pos.Item1, pos.Item2, info.mapSizeW, info.mapSizeH, pos.Item3, circle.spaceNoSub, bigarea);

            MapCircleHeight = (int)rect.Height;
            MapCircleWidth = (int)rect.Width;
            OnPropertyChanged(new PropertyChangedEventArgs("MapCircleHeight"));
            OnPropertyChanged(new PropertyChangedEventArgs("MapCircleWidth"));

            int offset_area_x = (bigarea == '東') ? 0 : 8 * info.mapSizeW - 1;
            int offset_area_y = (bigarea == '東') ? 1 + info.mapSizeH : 0;

            MapSelectedMargin = new Thickness(rect.X + offset_area_x, rect.Y + offset_area_y, 0.0, 0.0);
            OnPropertyChanged(new PropertyChangedEventArgs("MapSelectedMargin"));

            BitmapImage mapImg = await CatalogData.GetMap(circle.day, circle.blockId);
            MapImage = mapImg;
            OnPropertyChanged(new PropertyChangedEventArgs("MapImage"));
        }
        #endregion (SetCircleDetail)
        //-------------------------------------------------------------------------------
        #region +[async]UpdateBlockComboBox Blockコンボボックス設定
        //-------------------------------------------------------------------------------
        //
        public async Task UpdateBlockComboBox(int day) {
            Block_Data = await CatalogData.GetBlocksOfDay(day);
            OnPropertyChanged(new PropertyChangedEventArgs("Block_Data"));
        }
        #endregion (UpdateBlockComboBox)

        //-------------------------------------------------------------------------------
        #region +GetItems 要素データ取得
        //-------------------------------------------------------------------------------
        //
        public void GetItems(int index) {
            _vlist.GetBlockByIndex(index);
        }
        #endregion (GetItems)
        //-------------------------------------------------------------------------------
        #region +[async]GetIndexOfCircle サークルのインデックス取得
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetIndexOfCircle(int updateId) {
            int index = await CatalogData.GetIdOfCircle(updateId);
            _vlist.GetBlockByIndex(index);
            return index;
        }
        #endregion (GetIndexOfCircle)
        //-------------------------------------------------------------------------------
        #region +[async]GetIndexOfSelectedCheckListCircle 選択中チェックリストアイテムのインデックス
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetIndexOfSelectedCheckListCircle() {
            return await GetIndexOfCircle(SelectedCheckListItem.SerialNo);
        }
        #endregion (GetIndexOfSelectedCheckListCircle)
        //-------------------------------------------------------------------------------
        #region +[async]GetGridIndex
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetGridIndex(int dayIndex) {
            int index = await CatalogData.GetFirstCircleIndexOfDay(dayIndex);
            _vlist.GetBlockByIndex(index);
            return index;
        }
        public async Task<int> GetGridIndex(int dayIndex, char block) {
            int index = await CatalogData.GetFirstCircleIndexOfDayAndBlock(dayIndex, block);
            _vlist.GetBlockByIndex(index);
            return index;
        }
        public async Task<int> GetGridIndex(int dayIndex, string area) {
            var firstBlock = await CatalogData.GetFirstBlock(dayIndex, area);
            return await GetGridIndex(dayIndex, firstBlock);
        }
        public async Task<int> GetGridIndex(string genre) {
            int index = await CatalogData.GetFirstCircleIndexOfGenre(genre);
            _vlist.GetBlockByIndex(index);
            return index;
        }
        #endregion (+[async])

        public async Task<string> ConvertGenreIdToGenreStr(int id) { return await CatalogData.GetGenreStringById(id); }

        //-------------------------------------------------------------------------------
        #region -GetImages 画像取得(別スレッド)
        //-------------------------------------------------------------------------------
        //
        private async void GetImages(ThumbDispData[] data) {
            foreach (var d in data) {
                await d.GetImage();
            }
        }
        #endregion (GetImages)

        //-------------------------------------------------------------------------------
        #region #OnPropertyChanged
        //-------------------------------------------------------------------------------
        //
        protected void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion (#OnPropertyChanged)

        // チェックリスト
        //-------------------------------------------------------------------------------
        #region -[async]OpenCheckList チェックリストを開いて上書き
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> OpenCheckList(StorageFile file)
        {
            bool successed = await CheckList.OpenCheckList(file, CatalogData.GetDayIndex, CatalogData.GetCircleFromId);
            OnPropertyChanged(new PropertyChangedEventArgs("CheckList"));

            if (successed) {
                // save Checklist
                await CheckList.SaveCheckListToLocal();
            }

            return successed;
        }
        #endregion (OpenCheckList)
        //-------------------------------------------------------------------------------
        #region -CheckList_PropertyChanged
        //-------------------------------------------------------------------------------
        //
        private void CheckList_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "CircleList_Day1":
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1_Sorted"));
                    break;
                case "CircleList_Day2":
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2_Sorted"));
                    break;
                case "CircleList_Day3":
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3_Sorted"));
                    break;
                case "CircleList_Unknown":
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Unknown_Sorted"));
                    break;
            }
        }
        #endregion (CheckList_PropertyChanged)

        //-------------------------------------------------------------------------------
        #region +SetSelectedCheckListItem チェックリスト項目選択
        //-------------------------------------------------------------------------------
        //
        public async Task SetSelectedCheckListItem(CheckFileCircleInfo item) {
            // サークルデータ表示用設定
            SelectedCheckListItem = item;
            SelectedCheckListUnknownItem = null;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItem"));

            if (item == null) { return; }

            // Undo用
            if (CheckList.TimeData.ContainsKey(SelectedCheckListItem.SerialNo)) {
                var val = CheckList.TimeData[SelectedCheckListItem.SerialNo];
                _prev_memo = val.Memo;
                _prevDateTime_startline = val.Time_startLine;
                _prevDateTime_bought = val.Time_bought;
            }
            else {
                _prev_memo = null;
                _prevDateTime_startline = null;
                _prevDateTime_bought = null;
            }

            // 時刻表示用設定
            SelectedCheckListItemMemo = _prev_memo;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemMemo"));
            SelectedCheckListItemTime_startLine = _prevDateTime_startline;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_startLine"));
            SelectedCheckListItemTime_bought = _prevDateTime_bought;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_bought"));

            // サークルカット
            MapSelectedCircleCut = await CatalogData.GetCircleCutImageAsync(item.SerialNo);
            OnPropertyChanged(new PropertyChangedEventArgs("MapSelectedCircleCut"));
        }
        #endregion (+SetSelectedCheckListItem)
        #region +SetSelectedCheckListItem Unknownバージョン
        //-------------------------------------------------------------------------------
        //
        public void SetSelectedCheckListItem(CheckFileUnknownCircleInfo cfuci) {
            SelectedCheckListItem = null;
            SelectedCheckListUnknownItem = cfuci;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItem"));
        }
        #endregion (SetSelectedCheckListItem)
        //-------------------------------------------------------------------------------
        #region +UpdateDateTimeInfo 時刻更新
        //-------------------------------------------------------------------------------
        //
        public void UpdateDateTimeInfo(DateTimeType type) {
            DateTime dt = DateTime.Now;
            switch (type) {
                case DateTimeType.並び始め:
                    SelectedCheckListItemTime_startLine = dt;
                    OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_startLine"));
                    break;
                case DateTimeType.購入完了:
                    SelectedCheckListItemTime_bought = dt;
                    OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_bought"));
                    break;
            }
        }
        #endregion (UpdateDateTimeInfo)
        //-------------------------------------------------------------------------------
        #region +UndoDateTimeInfo 時刻リセット
        //-------------------------------------------------------------------------------
        //
        public void UndoDateTimeInfo() {
            SelectedCheckListItemMemo = _prev_memo;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemMemo"));
            SelectedCheckListItemTime_startLine = _prevDateTime_startline;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_startLine"));
            SelectedCheckListItemTime_bought = _prevDateTime_bought;
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItemTime_bought"));
        }
        #endregion (UndoDateTimeInfo)
        //-------------------------------------------------------------------------------
        #region +SaveSelectedCheckListItem チェックリスト項目更新と保存
        //-------------------------------------------------------------------------------
        //
        public async Task SaveSelectedCheckListItem() {
            Func<bool> changed = () => (_prev_memo != SelectedCheckListItemMemo)
                                      || !_prevDateTime_startline.Equals(SelectedCheckListItemTime_startLine)
                                      || !_prevDateTime_bought.Equals(SelectedCheckListItemTime_bought);

            if (changed()) {
                int serNo = SelectedCheckListItem.SerialNo;

                var clt = new CheckListTimeData();
                clt.Memo = SelectedCheckListItemMemo;
                clt.Time_startLine = SelectedCheckListItemTime_startLine;
                clt.Time_bought = SelectedCheckListItemTime_bought;

                if (!CheckList.TimeData.ContainsKey(serNo)) {
                    CheckList.TimeData.Add(serNo, clt);
                }
                else {
                    CheckList.TimeData[SelectedCheckListItem.SerialNo] = clt;
                }

                await CheckList.SaveTimeData();
            }
        }
        #endregion (SaveSelectedCheckListItem)

        //-------------------------------------------------------------------------------
        #region +AddCircleToCheckList チェックリストにサークル追加
        //-------------------------------------------------------------------------------
        //
        public async Task AddCircleToCheckList(ComiketCircle circle, int colorIndex) {
            var prev = CheckList.GetCircleInfo(circle.updateId);
            if (prev != null) {
                CheckList.ChangeColor(prev, colorIndex);
            }
            else {
                CheckFileCircleInfo cfci = new CheckFileCircleInfo() {
                    ColorIndex = colorIndex,
                    GenreCode = await CatalogData.GetGenreCode(await CatalogData.GetGenreStringById(circle.genreId)),
                    SpaceNo = circle.spaceNo,
                    SpaceNoSub = circle.spaceNoSub,
                    CircleName = circle.circleName,
                    CircleKana = circle.circleKana,
                    BookName = circle.bookName,
                    PenName = circle.penName,
                    CutIndex = circle.cutIndex,
                    PageNo = circle.pageNo,
                    Description = circle.description,
                    Mail = circle.mailAddr,
                    URL = circle.url,
                    Url_Circlems = circle.circlems,
                    RSS = circle.rss,
                    SerialNo = circle.updateId,
                    Memo = "",
                    UpdateInfo = "",
                    RSSgetInfo = ""
                };

                cfci.Block = await CatalogData.GetBlockChr(circle.blockId);
                cfci.Area = await CatalogData.GetAreaChar(circle.blockId);
                var dayofweek = (await CatalogData.GetDays())[circle.day - 1];
                cfci.DayOfWeek = CatalogData.DayOfWeekToChrDic[dayofweek];

                var map = await CatalogData.GetMapPlace(circle.blockId, circle.spaceNo);
                cfci.MapX = map.Item1;
                cfci.MapY = map.Item2;
                cfci.Layout = map.Item3;

                CheckList.AddCircle(cfci, circle.day);
            }

            // save Checklist
            await CheckList.SaveCheckListToLocal();
        }
        #endregion (AddCircleToCheckList)

        //-------------------------------------------------------------------------------
        #region +RemoveSelectedCheckListItem 選択中チェックリストアイテムの削除
        //-------------------------------------------------------------------------------
        //
        public async Task RemoveSelectedCheckListItem() {
            if (SelectedCheckListItem != null) {
                CheckList.RemoveItem(SelectedCheckListItem);
            }
            else if (SelectedCheckListUnknownItem != null) {
                CheckList.RemoveItem(SelectedCheckListUnknownItem);
            }

            // save Checklist
            await CheckList.SaveCheckListToLocal();
        }
        #endregion (RemoveSelectedCheckListItem)

        //-------------------------------------------------------------------------------
        #region +GetCurrentSelectedCheckListItemColor 選択中チェックリストアイテムの色インデックス
        //-------------------------------------------------------------------------------
        //
        public int GetCurrentSelectedCheckListItemColorIndex() {
            if (SelectedCheckListItem != null) {
                return SelectedCheckListItem.ColorIndex;
            }
            else if (SelectedCheckListUnknownItem != null) {
                return SelectedCheckListUnknownItem.ColorIndex;
            }
            return -1;
        }
        #endregion (GetCurrentSelectedCheckListItemColor)
        //-------------------------------------------------------------------------------
        #region +[async]ChangeSelectedCheckListItemColor 選択中チェックリストアイテムの色を変更
        //-------------------------------------------------------------------------------
        //
        public async Task ChangeSelectedCheckListItemColor(int colorIndex) {
            if (SelectedCheckListItem != null) {
                CheckList.ChangeColor(SelectedCheckListItem, colorIndex);
                OnPropertyChanged(new PropertyChangedEventArgs("SelectedCheckListItem"));
            }
            else if (SelectedCheckListUnknownItem != null) {
                CheckList.ChangeColor(SelectedCheckListUnknownItem, colorIndex);
            }

            // save Checklist
            await CheckList.SaveCheckListToLocal();
        }
        #endregion (ChangeSelectedCheckListItemColor)

        //-------------------------------------------------------------------------------
        #region +RefreshSelectedCheckListItem  選択中チェックリストアイテムの表示を更新
        //-------------------------------------------------------------------------------
        //
        public void RefreshSelectedCheckListItem() {
            if (SelectedCheckListItem != null) {
                CheckList.RefreshItem(SelectedCheckListItem);
            }
        }
        #endregion (+RefreshSelectedCheckListItem)

        //-------------------------------------------------------------------------------
        #region -(static class)Sorting サークルのソーティング用メソッド
        //-------------------------------------------------------------------------------
        public static class Sorting
        {
            public static CompareColorIndexAsc compColorIndexAsc = new CompareColorIndexAsc();
            public static ComparePlaceAsc compPlaceAsc = new ComparePlaceAsc();
            public static CompareColorIndexDesc compColorIndexDesc = new CompareColorIndexDesc();
            public static ComparePlaceDesc compPlaceDesc = new ComparePlaceDesc();

            public static IOrderedEnumerable<CheckFileCircleInfo> SortColor_Asc(IEnumerable<CheckFileCircleInfo> info)
            {
                return info.OrderBy(cfci => cfci.ColorIndex, compColorIndexAsc);
            }
            public static IOrderedEnumerable<CheckFileCircleInfo> SortColor_Desc(IEnumerable<CheckFileCircleInfo> info)
            {
                return info.OrderByDescending(cfci => cfci.ColorIndex, compColorIndexAsc);
            }
            public static IOrderedEnumerable<CheckFileCircleInfo> SortPlace_Asc(IEnumerable<CheckFileCircleInfo> info)
            {
                return info.OrderBy(cfci => cfci, compPlaceAsc);
            }
            public static IOrderedEnumerable<CheckFileCircleInfo> SortPlace_Desc(IEnumerable<CheckFileCircleInfo> info)
            {
                return info.OrderByDescending(cfci => cfci, compPlaceAsc);
            }

            public static IOrderedEnumerable<CheckFileUnknownCircleInfo> SortColor_Asc(IEnumerable<CheckFileUnknownCircleInfo> info)
            {
                return info.OrderBy(cfci => cfci.ColorIndex, compColorIndexAsc);
            }
            public static IOrderedEnumerable<CheckFileUnknownCircleInfo> SortColor_Desc(IEnumerable<CheckFileUnknownCircleInfo> info)
            {
                return info.OrderByDescending(cfci => cfci.ColorIndex, compColorIndexAsc);
            }

            public class CompareColorIndexAsc : IComparer<int>, IComparer<CheckFileCircleInfo>, IComparer<CheckFileUnknownCircleInfo>
            {
                public int Compare(int x, int y)
                {
                    if (x <= 0) { x = int.MaxValue; }
                    if (y <= 0) { y = int.MaxValue; }
                    return x.CompareTo(y);
                }

                public int Compare(CheckFileCircleInfo x, CheckFileCircleInfo y)
                {
                    return Compare(x.ColorIndex, y.ColorIndex);
                }

                public int Compare(CheckFileUnknownCircleInfo x, CheckFileUnknownCircleInfo y)
                {
                    return Compare(x.ColorIndex, y.ColorIndex);
                }
            }

            public class CompareColorIndexDesc : IComparer<int>, IComparer<CheckFileCircleInfo>, IComparer<CheckFileUnknownCircleInfo>
            {
                public int Compare(int x, int y) { return Sorting.compColorIndexAsc.Compare(y, x); }
                public int Compare(CheckFileCircleInfo x, CheckFileCircleInfo y) { return Compare(x.ColorIndex, y.ColorIndex); }
                public int Compare(CheckFileUnknownCircleInfo x, CheckFileUnknownCircleInfo y) { return Compare(x.ColorIndex, y.ColorIndex); }
            }

            public class ComparePlaceAsc : IComparer<CheckFileCircleInfo>
            {
                public int Compare(CheckFileCircleInfo x, CheckFileCircleInfo y)
                {
                    int blockComp = ComiketUtil.CompareBlock(x.Block, y.Block);
                    if (blockComp != 0) { return blockComp; }

                    int spaceNoComp = x.SpaceNo.CompareTo(y.SpaceNo);
                    if (spaceNoComp != 0) { return spaceNoComp; }

                    return x.SpaceNoSub.CompareTo(y.SpaceNoSub);
                }
            }
            public class ComparePlaceDesc : IComparer<CheckFileCircleInfo>
            {
                public int Compare(CheckFileCircleInfo x, CheckFileCircleInfo y)
                {
                    return Sorting.compPlaceAsc.Compare(y, x);
                }
            }
        }
        //-------------------------------------------------------------------------------
        #endregion (-(static class)Sorting サークルのソーティング用メソッド)
        //-------------------------------------------------------------------------------
        #region +UpdateSortingOfCheckList チェックリストのソーティング設定
        //-------------------------------------------------------------------------------
        //
        public void UpdateSortingOfCheckList()
        {
            if (CheckList == null) { return; }
            CheckListSortingType sortType = SelectedSortType;
            switch (sortType) {
                case CheckListSortingType.None:
                    CheckList.SetCheckListComparer(null, null);
                    break;
                case CheckListSortingType.Color_Asc:
                    CheckList.SetCheckListComparer(Sorting.compColorIndexAsc, Sorting.compColorIndexAsc);
                    break;
                case CheckListSortingType.Color_Desc:
                    CheckList.SetCheckListComparer(Sorting.compColorIndexDesc, Sorting.compColorIndexDesc);
                    break;
                case CheckListSortingType.Place_Asc:
                    CheckList.SetCheckListComparer(Sorting.compPlaceAsc, null);
                    break;
                case CheckListSortingType.Place_Desc:
                    CheckList.SetCheckListComparer(Sorting.compPlaceDesc, null);
                    break;
            }

            /*switch (sortType) {
                case CheckListSortingType.None:
                    CircleList_Sort = null;
                    CircleList_Unknown_Sort = null;
                    break;
                case CheckListSortingType.Color_Asc:
                    CircleList_Sort = Sorting.SortColor_Asc;
                    CircleList_Unknown_Sort = Sorting.SortColor_Asc;
                    break;
                case CheckListSortingType.Color_Desc:
                    CircleList_Sort = Sorting.SortColor_Desc;
                    CircleList_Unknown_Sort = Sorting.SortColor_Desc;
                    break;
                case CheckListSortingType.Place_Asc:
                    CircleList_Sort = Sorting.SortPlace_Asc;
                    CircleList_Unknown_Sort = null;
                    break;
                case CheckListSortingType.Place_Desc:
                    CircleList_Sort = Sorting.SortPlace_Desc;
                    CircleList_Unknown_Sort = null;
                    break;
            }

            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1_Sorted"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2_Sorted"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3_Sorted"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Unknown_Sorted"));*/
        }
        #endregion (UpdateSortingOfCheckList)
    }
    //-------------------------------------------------------------------------------
    #endregion ((class)ModelView)

    //-------------------------------------------------------------------------------
    #region DateTimeType 列挙体：
    //-------------------------------------------------------------------------------
    /// <summary>
    /// 
    /// </summary>
    public enum DateTimeType {
        /// <summary></summary>
        並び始め,
        購入完了
    }
    //-------------------------------------------------------------------------------
    #endregion (DateTimeType)

    //===============================================================================
    #region (class)ThumbDispData
    //-------------------------------------------------------------------------------
    public class ThumbDispData : INotifyPropertyChanged {
        public BitmapImage Image { get; private set; }
        public string Text { get; private set; }
        public ComiketCircle CircleData { get; private set; }
        private int _updateId;
        Func<int, Brush> _getBrushFunc;
        public Brush CheckListColor { get { return _getBrushFunc(CircleData.updateId); } }

        public ThumbDispData(string text, int updateId, ComiketCircle cc, CheckList checklist) {
            Text = text;
            Image = null;
            _updateId = updateId;
            CircleData = cc;
            _getBrushFunc = checklist.GetCircleBrush;
            checklist.PropertyChanged += checklist_PropertyChanged;
        }

        private void checklist_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "CircleList_Day1":
                case "CircleList_Day2":
                case "CircleList_Day3":
                    OnPropertyChanged(new PropertyChangedEventArgs("CheckListColor"));
                    break;
            }
        }

        public async Task GetImage() {
            try {
                var img = await CatalogData.GetCircleCutImageAsync(_updateId);
                Image = img;
                OnPropertyChanged(new PropertyChangedEventArgs("Image"));
            }
            catch (Exception) { }
        }

        public async void GetImageAsync() {
            await GetImage();
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    //-------------------------------------------------------------------------------
    #endregion ((class)ThumbDispData)

    //-------------------------------------------------------------------------------
    #region (class)CircleDetailDispData
    //-------------------------------------------------------------------------------
    class CircleDetailDispData : INotifyPropertyChanged {
        public string Place { get; private set; }
        public string CircleName { get; private set; }
        public string CircleKana { get; private set; }
        public string BookName { get; private set; }
        public string Genre { get; private set; }
        public string PenName { get; private set; }
        public string Description { get; private set; }
        public string Mail { get; private set; }
        public string URL { get; private set; }
        public string Memo { get; private set; }
        public string Url_Circlems { get; private set; }

        public static async Task<CircleDetailDispData> ConvertToCircleDetailDispData(ComiketCircle cc, Func<int, Task<char>> blockIdToChrFunc, Func<int, Task<string>> genreIdToStrFunc) {
            char block = await blockIdToChrFunc(cc.blockId);
            string genre = await genreIdToStrFunc(cc.genreId);

            return new CircleDetailDispData() {
                Place = string.Format("{0}日目 {1}{2}", cc.day, block, cc.SpaceStr),
                CircleName = cc.circleName,
                CircleKana = cc.circleKana,
                BookName = cc.bookName,
                Genre = genre,
                PenName = cc.penName,
                Description = cc.description,
                Mail = cc.mailAddr,
                URL = cc.url,
                Memo = cc.memo,
                Url_Circlems = cc.circlems
            };
        }

        private CircleDetailDispData() { }

        protected void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(e.PropertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    //-------------------------------------------------------------------------------
    #endregion ((class)CircleDetailDispData)


    //-------------------------------------------------------------------------------
    #region CheckListSortingType 列挙体
    //-------------------------------------------------------------------------------
    /// <summary>
    /// 
    /// </summary>
    public enum CheckListSortingType {
        /// <summary></summary>
        None,
        Color_Asc,
        Color_Desc,
        Place_Asc,
        Place_Desc
    }
    //-------------------------------------------------------------------------------
    #endregion (CheckListSortingType)
    //-------------------------------------------------------------------------------
    #region (static class)CheckListSortingTypeUtil
    //-------------------------------------------------------------------------------
    public static class CheckListSortingTypeUtil {
        //-------------------------------------------------------------------------------
        #region +[static]ConvertToString
        //-------------------------------------------------------------------------------
        //
        public static string ConvertToString(CheckListSortingType sortType) {
            switch (sortType) {
                case CheckListSortingType.None:
                    return "なし";
                case CheckListSortingType.Color_Asc:
                    return "色(昇順)";
                case CheckListSortingType.Color_Desc:
                    return "色(降順)";
                case CheckListSortingType.Place_Asc:
                    return "場所(昇順)";
                case CheckListSortingType.Place_Desc:
                    return "場所(降順)";
                default:
                    return "";
            }
        }
        #endregion (ConvertToString)
        //-------------------------------------------------------------------------------
        #region +[static]ConvertFromString
        //-------------------------------------------------------------------------------
        //
        public static CheckListSortingType ConvertFromString(string str) {
            switch (str) {
                case "なし":
                    return CheckListSortingType.None;
                case "色(昇順)":
                    return CheckListSortingType.Color_Asc;
                case "色(降順)":
                    return CheckListSortingType.Color_Desc;
                case "場所(昇順)":
                    return CheckListSortingType.Place_Asc;
                case "場所(降順)":
                    return CheckListSortingType.Place_Desc;
                default:
                    return CheckListSortingType.None;
            }
        }
        #endregion (ConvertFromString)
    }
    //-------------------------------------------------------------------------------
    #endregion ((static class)CheckListSortingTypeUtil )
    //-------------------------------------------------------------------------------
    #region (class)SortingTypesToStringsConverter
    //-------------------------------------------------------------------------------
    public class SortingTypesToStringsConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, string language) {
            CheckListSortingType[] values = (CheckListSortingType[])value;
            return values.Select(CheckListSortingTypeUtil.ConvertToString).ToArray();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }
    //-------------------------------------------------------------------------------
    #endregion (SortingTypesToStringsConverter)
    #region (class)SortingTypeToStringConverter
    //-------------------------------------------------------------------------------
    public class SortingTypeToStringConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, string language) {
            CheckListSortingType val = (CheckListSortingType)value;
            return CheckListSortingTypeUtil.ConvertToString(val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            string val = (string)value;
            return CheckListSortingTypeUtil.ConvertFromString(val);
        }
    }
    //-------------------------------------------------------------------------------
    #endregion (SortingTypeToStringConverter)
}
