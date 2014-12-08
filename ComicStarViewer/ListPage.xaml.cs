using ComicStarViewer.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// 基本ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234237 を参照してください

namespace ComicStarViewer
{
    /// <summary>
    /// 多くのアプリケーションに共通の特性を指定する基本ページ。
    /// </summary>
    public sealed partial class ListPage : Page
    {

        //-------------------------------------------------------------------------------
        #region Util
        //-------------------------------------------------------------------------------
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        /// <summary>
        /// これは厳密に型指定されたビュー モデルに変更できます。
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// NavigationHelper は、ナビゲーションおよびプロセス継続時間管理を
        /// 支援するために、各ページで使用します。
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }
        //-------------------------------------------------------------------------------
        #endregion (Util)

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public ListPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;

            CANVASES = new Canvas[] {
                    canvasMap1_E123, canvasMap1_E456, canvasMap1_W,
                    canvasMap2_E123, canvasMap2_E456, canvasMap2_W,
                    canvasMap3_E123, canvasMap3_E456, canvasMap3_W
                };
            SCROLLVIEWERS = new ScrollViewer[] {
                    scImgMap1_E123, scImgMap1_E456, scImgMap1_W,
                    scImgMap2_E123, scImgMap2_E456, scImgMap2_W,
                    scImgMap3_E123, scImgMap3_E456, scImgMap3_W
                };
            SELECT_BORDERS = new Border[] {
                borderMap1_E123, borderMap1_E456, borderMap1_W,
                borderMap2_E123, borderMap2_E456, borderMap2_W,
                borderMap3_E123, borderMap3_E456, borderMap3_W
            };
            RADIOBUTTONS = new RadioButton[] {
                rbSearch_Checklist, rbSearch_SearchString, rbSearch_Genre
            };
        }
        #endregion (Constructor)

        private readonly Canvas[] CANVASES;
        private readonly ScrollViewer[] SCROLLVIEWERS;
        private readonly Border[] SELECT_BORDERS;
        private readonly RadioButton[] RADIOBUTTONS;

        private CatalogData _catalogData;
        private CheckList _checkList;
        private IndicatorColorController _indColorController = new IndicatorColorController();

        private string _mapName1, _mapName2, _mapName3;

        private Dictionary<int, Rectangle> _serialNoToRect = new Dictionary<int, Rectangle>();
        private Dictionary<Rectangle, int> _rectToMapIndex = new Dictionary<Rectangle, int>();

        private bool _suspendMove = false;

        //-------------------------------------------------------------------------------
        #region navigationHelper_LoadState
        //-------------------------------------------------------------------------------
        //
        /// <summary>
        /// このページには、移動中に渡されるコンテンツを設定します。前のセッションからページを
        /// 再作成する場合は、保存状態も指定されます。
        /// </summary>
        /// <param name="sender">
        /// イベントのソース (通常、<see cref="NavigationHelper"/>)>
        /// </param>
        /// <param name="e">このページが最初に要求されたときに
        /// <see cref="Frame.Navigate(Type, Object)"/> に渡されたナビゲーション パラメーターと、
        /// 前のセッションでこのページによって保存された状態の辞書を提供する
        /// セッション。ページに初めてアクセスするとき、状態は null になります。</param>
        private async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            //var param = e.NavigationParameter as Tuple<CatalogData, CheckList>;
            //_catalogData = (param != null) ? param.Item1 : null;
            //_checkList = (param != null) ? param.Item2 : null;

            _catalogData = await CatalogData.OpenCatalogData();

            var info = await _catalogData.GetComiketInfo();
            _checkList = new CheckList(info.comiketNo);
            if (await CheckList.CheckExistCheckList()) {
                await _checkList.OpenCheckList(await CheckList.OpenExistCheckList(), _catalogData.GetDayIndex, _catalogData.GetCircleFromId);
            }
            //_checkList.PropertyChanged += CheckList_PropertyChanged;

            if (_catalogData != null) {
                var names = await _catalogData.GetMapNames();
                if (names.Length >= 3) {
                    this.defaultViewModel["MapName1"] = _mapName1 = names[0];
                    this.defaultViewModel["MapName2"] = _mapName2 = names[1];
                    this.defaultViewModel["MapName3"] = _mapName3 = names[2];
                }

                var maps = await _catalogData.GetAllMaps();
                this.defaultViewModel["MapImage1_E123"] = maps[0];
                this.defaultViewModel["MapImage1_E456"] = maps[1];
                this.defaultViewModel["MapImage1_W"] = maps[2];
                this.defaultViewModel["MapImage2_E123"] = maps[3];
                this.defaultViewModel["MapImage2_E456"] = maps[4];
                this.defaultViewModel["MapImage2_W"] = maps[5];
                this.defaultViewModel["MapImage3_E123"] = maps[6];
                this.defaultViewModel["MapImage3_E456"] = maps[7];
                this.defaultViewModel["MapImage3_W"] = maps[8];

                this.defaultViewModel["MapWidth_E123"] = maps[0].PixelWidth;
                this.defaultViewModel["MapHeight_E123"] = maps[0].PixelHeight;
                this.defaultViewModel["MapWidth_E456"] = maps[1].PixelWidth;
                this.defaultViewModel["MapHeight_E456"] = maps[1].PixelHeight;
                this.defaultViewModel["MapWidth_W"] = maps[2].PixelWidth;
                this.defaultViewModel["MapHeight_W"] = maps[2].PixelHeight;

                var genremaps = await _catalogData.GetAllGenreInfoMaps();
                this.defaultViewModel["MapGenreImage1_E123"] = genremaps[0];
                this.defaultViewModel["MapGenreImage1_E456"] = genremaps[1];
                this.defaultViewModel["MapGenreImage1_W"] = genremaps[2];
                this.defaultViewModel["MapGenreImage2_E123"] = genremaps[3];
                this.defaultViewModel["MapGenreImage2_E456"] = genremaps[4];
                this.defaultViewModel["MapGenreImage2_W"] = genremaps[5];
                this.defaultViewModel["MapGenreImage3_E123"] = genremaps[6];
                this.defaultViewModel["MapGenreImage3_E456"] = genremaps[7];
                this.defaultViewModel["MapGenreImage3_W"] = genremaps[8];

                var genres = await _catalogData.GetGenres();
                this.defaultViewModel["Genres"] = genres;
                if (genres.Length > 0) { cmbGenre.SelectedIndex = 0; }
            }

            this.defaultViewModel["ColorController"] = _indColorController;

            if (_checkList == null) {
                rbSearch_Checklist.IsEnabled = false;
                rbSearch_SearchString.IsChecked = true;
            }
            else {
                rbSearch_Checklist.IsChecked = true;
            }

            if (e.PageState != null) {
                if (e.PageState.ContainsKey("CheckListFilterText")) {
                    txtCheckListFilter.Text = e.PageState["CheckListFilterText"].ToString();
                }
                if (e.PageState.ContainsKey("SearchStringText")) {
                    txtSearchString.Text = e.PageState["SearchStringText"].ToString();
                }
                if (e.PageState.ContainsKey("GenreText")) {
                    cmbGenre.SelectedItem = e.PageState["GenreText"].ToString();
                }
                if (e.PageState.ContainsKey("SelectedRadioButton")) {
                    RadioButton rb = RADIOBUTTONS[int.Parse(e.PageState["SelectedRadioButton"].ToString())];
                    rb.IsChecked = true;
                }
                //e.PageState["IsSearched"] = false;
            }
        }
        #endregion (navigationHelper_LoadState)
        //-------------------------------------------------------------------------------
        #region navigationHelper_SaveState
        //-------------------------------------------------------------------------------
        //
        /// <summary>
        /// アプリケーションが中断される場合、またはページがナビゲーション キャッシュから破棄される場合、
        /// このページに関連付けられた状態を保存します。値は、
        /// <see cref="SuspensionManager.SessionState"/> のシリアル化の要件に準拠する必要があります。
        /// </summary>
        /// <param name="sender">イベントのソース (通常、<see cref="NavigationHelper"/>)</param>
        /// <param name="e">シリアル化可能な状態で作成される空のディクショナリを提供するイベント データ
        ///。</param>
        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtCheckListFilter.Text)) {
                e.PageState["CheckListFilterText"] = txtCheckListFilter.Text;
            }
            if (!string.IsNullOrWhiteSpace(txtSearchString.Text)) {
                e.PageState["SearchStringText"] = txtSearchString.Text;
            }
            if (cmbGenre.SelectedItem != null) {
                e.PageState["GenreText"] = cmbGenre.SelectedItem.ToString();
            }
            e.PageState["SelectedRadioButton"] = RADIOBUTTONS.ToList().IndexOf(RADIOBUTTONS.FirstOrDefault(rb => rb.IsChecked.HasValue && rb.IsChecked.Value));
            e.PageState["IsSearched"] = false;
        }
        #endregion (navigationHelper_SaveState)

        #region NavigationHelper の登録

        /// このセクションに示したメソッドは、NavigationHelper がページの
        /// ナビゲーション メソッドに応答できるようにするためにのみ使用します。
        /// 
        /// ページ固有のロジックは、
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// および <see cref="GridCS.Common.NavigationHelper.SaveState"/> のイベント ハンドラーに配置する必要があります。
        /// LoadState メソッドでは、前のセッションで保存されたページの状態に加え、
        /// ナビゲーション パラメーターを使用できます。

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }

        #endregion

        //-------------------------------------------------------------------------------
        #region rbSearch_Checked ラジオボタンタップ時
        //-------------------------------------------------------------------------------
        //
        private void rbSearch_Checked(object sender, RoutedEventArgs e)
        {

        }
        #endregion (rbSearch_Checked)
        //-------------------------------------------------------------------------------
        #region buttonSearch_Click 表示クリック
        //-------------------------------------------------------------------------------
        //
        private async void buttonSearch_Click(object sender, RoutedEventArgs e)
        {
            buttonSearch.IsEnabled = false;
            progressringDisp.IsActive = true;

            List<DispCircleInfo> list;
            bool existMemo;
            if (rbSearch_Checklist.IsChecked.HasValue && rbSearch_Checklist.IsChecked.Value) {
                list = await GetCheckList(txtCheckListFilter.Text);
                existMemo = true;
            }
            else if (rbSearch_SearchString.IsChecked.HasValue && rbSearch_SearchString.IsChecked.Value) {
                list = await GetSearchResult(txtSearchString.Text);
                existMemo = false;
            }
            else if (rbSearch_Genre.IsChecked.HasValue && rbSearch_Genre.IsChecked.Value) {
                list = await GetGenreSearchResult(cmbGenre.SelectedValue as string);
                existMemo = false;
            }
            else { return; }

            this.defaultViewModel["DetailMemoVisible"] = (existMemo) ? Visibility.Visible : Visibility.Collapsed;

            await Display(list);

            buttonSearch.IsEnabled = true;
            progressringDisp.IsActive = false;
        }
        #endregion (buttonSearch_Click)

        //-------------------------------------------------------------------------------
        #region -GetCheckList チェックリストデータ取得
        //-------------------------------------------------------------------------------
        //
        private async Task<List<DispCircleInfo>> GetCheckList(string queryStr)
        {
            var list = new List<DispCircleInfo>();
            var items = (string.IsNullOrEmpty(queryStr)) ? _checkList.CircleList_Day1.Concat(_checkList.CircleList_Day2).Concat(_checkList.CircleList_Day3)
                                                         : _checkList.CircleList_Day1.Where(cfci => cfci.Memo.Contains(queryStr))
                                                               .Concat(_checkList.CircleList_Day2.Where(cfci => cfci.Memo.Contains(queryStr)))
                                                               .Concat(_checkList.CircleList_Day3.Where(cfci => cfci.Memo.Contains(queryStr)));
            foreach (var item in items) {
                var element = new DispCircleInfo()
                {
                    Block = item.Block,
                    Area = await _catalogData.GetAreaChar(item.Block),
                    Day = await _catalogData.GetDayIndex(item.DayOfWeek),
                    CircleName = item.CircleName,
                    CircleKana = item.CircleKana,
                    PenName = item.PenName,
                    BookName = item.BookName,
                    Description = item.Description,
                    URL = item.URL,
                    GenreStr = await _catalogData.GetGenreStringByCode(item.GenreCode),
                    Mail = item.Mail,
                    SpaceNo = item.SpaceNo,
                    SpaceNoSub = item.SpaceNoSub,

                    Memo = item.Memo,

                    MapX = item.MapX,
                    MapY = item.MapY,
                    Layout = item.Layout,

                    SerialNo = item.SerialNo,

                    ColorIndex = item.ColorIndex,
                    IndexToColorFunc = _checkList.ColorIndexToColorFunc
                };

                list.Add(element);

            }
            return list;
        }
        #endregion (GetCheckList)
        //-------------------------------------------------------------------------------
        #region -GetSearchResult 文字列検索結果取得
        //-------------------------------------------------------------------------------
        //
        private async Task<List<DispCircleInfo>> GetSearchResult(string queryStr)
        {
            var res = await _catalogData.SearchCirclesByStr(queryStr);
            List<DispCircleInfo> list = new List<DispCircleInfo>();
            foreach (var cc in res) {
                var element = new DispCircleInfo()
                {
                    Block = await _catalogData.GetBlockChr(cc.blockId),
                    Area = await _catalogData.GetAreaChar(cc.blockId),
                    Day = cc.day,
                    CircleName = cc.circleName,
                    CircleKana = cc.circleKana,
                    PenName = cc.penName,
                    BookName = cc.bookName,
                    Description = cc.description,
                    URL = cc.url,
                    GenreStr = await _catalogData.GetGenreStringById(cc.genreId),
                    Mail = cc.mailAddr,
                    SpaceNo = cc.spaceNo,
                    SpaceNoSub = cc.spaceNoSub,

                    SerialNo = cc.updateId,

                    MapX = cc.xpos,
                    MapY = cc.ypos,
                    Layout = cc.layout
                };
                list.Add(element);
            }
            return list;
        }
        #endregion (GetSearchResult)
        //-------------------------------------------------------------------------------
        #region -GetGenreSearchResult ジャンル検索結果表示
        //-------------------------------------------------------------------------------
        //
        private async Task<List<DispCircleInfo>> GetGenreSearchResult(string genreStr)
        {
            var res = await _catalogData.SearchCirclesByGenre(genreStr);
            List<DispCircleInfo> list = new List<DispCircleInfo>();
            foreach (var cc in res) {
                var element = new DispCircleInfo()
                {
                    Block = await _catalogData.GetBlockChr(cc.blockId),
                    Area = await _catalogData.GetAreaChar(cc.blockId),
                    Day = cc.day,
                    CircleName = cc.circleName,
                    CircleKana = cc.circleKana,
                    PenName = cc.penName,
                    BookName = cc.bookName,
                    Description = cc.description,
                    URL = cc.url,
                    GenreStr = await _catalogData.GetGenreStringById(cc.genreId),
                    Mail = cc.mailAddr,
                    SpaceNo = cc.spaceNo,
                    SpaceNoSub = cc.spaceNoSub,

                    SerialNo = cc.updateId,

                    MapX = cc.xpos,
                    MapY = cc.ypos,
                    Layout = cc.layout
                };
                list.Add(element);
            }
            return list;
        }
        #endregion (GetGenreSearchResult)

        //-------------------------------------------------------------------------------
        #region -Display 表示
        //-------------------------------------------------------------------------------
        //
        private async Task Display(List<DispCircleInfo> info)
        {
            var cc = await _catalogData.GetComiketInfo();

            _serialNoToRect.Clear();
            _rectToMapIndex.Clear();
            Action<Canvas> deleteRects = (Canvas canvas) =>
            {
                for (int i = canvas.Children.Count - 1; i >= 0; --i) {
                    var child = canvas.Children[i];
                    if (child is Rectangle) {
                        canvas.Children.RemoveAt(i);
                    }
                }
            };

            deleteRects(canvasMap1_E123);
            deleteRects(canvasMap1_E456);
            deleteRects(canvasMap1_W);
            deleteRects(canvasMap2_E123);
            deleteRects(canvasMap2_E456);
            deleteRects(canvasMap2_W);
            deleteRects(canvasMap3_E123);
            deleteRects(canvasMap3_E456);
            deleteRects(canvasMap3_W);

            foreach (var item in info) {
                int canvasIndex = await GetCanvasIndex(item);

                if (canvasIndex == -1) { Debug.Assert(false); continue; }
                Canvas canvas = CANVASES[canvasIndex];

                var rect = ComiketUtil.GetCircleRect(item.MapX, item.MapY, cc.mapSizeW, cc.mapSizeH, item.Layout, item.SpaceNoSub, await _catalogData.GetAreaChar(item.Block));
                Rectangle rectangle = new Rectangle()
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Fill = item.MarkColor,
                    Opacity = 0.8
                };
                rectangle.SetValue(Canvas.LeftProperty, rect.Left);
                rectangle.SetValue(Canvas.TopProperty, rect.Top);
                rectangle.SetValue(Canvas.ZIndexProperty, 4);

                rectangle.Tag = item;
                rectangle.Tapped += rectangle_Tapped;

                canvas.Children.Add(rectangle);

                _rectToMapIndex.Add(rectangle, canvasIndex);
                _serialNoToRect.Add(item.SerialNo, rectangle);
            }


            this.defaultViewModel["Items"] = info.GroupBy(dci => dci.Day)
                                                 .OrderBy(grp => grp.Key);
        }
        #endregion (Display)
        //-------------------------------------------------------------------------------
        #region rectangle_Tapped
        //-------------------------------------------------------------------------------
        //
        private async void rectangle_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Rectangle rectangle = sender as Rectangle;
            if (rectangle == null) { return; }
            await ShowToolTipToRectangle(rectangle);
        }
        #endregion (rectangle_Tapped)

        //-------------------------------------------------------------------------------
        #region -ShowToolTipToRectangle サークルRectangleにFlyout表示
        //-------------------------------------------------------------------------------
        //
        private async Task ShowToolTipToRectangle(Rectangle rectangle)
        {
            var item = rectangle.Tag as DispCircleInfo;
            if (item == null) { return; }

            this.defaultViewModel["DetailTextPlace"] = item.SpaceStr;
            this.defaultViewModel["DetailTextCircleName"] = item.CircleName;
            this.defaultViewModel["DetailTextBookName"] = item.BookName;
            this.defaultViewModel["DetailTextGenre"] = item.GenreStr;
            this.defaultViewModel["DetailTextPenName"] = item.PenName;
            this.defaultViewModel["DetailTextDescription"] = item.Description;
            this.defaultViewModel["DetailTextURL"] = item.URL;
            this.defaultViewModel["DetailTextMemo"] = item.Memo;

            int canvasIndex = _rectToMapIndex[rectangle];
            flyout_detail.ShowAt(rectangle);

            var img = await CatalogData.GetCircleCutImageAsync(item.SerialNo);
            this.defaultViewModel["DetailImage"] = img;

            _suspendMove = true;
            listItems.SelectedValue = item;
            listItems.ScrollIntoView(item);
            _suspendMove = false;
        }
        #endregion (ShowToolTipToRectangle)

        //-------------------------------------------------------------------------------
        #region flipMap_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void flipMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FlipView flipView = sender as FlipView;
            if (flipView == null) { return; }
            _indColorController.setIndex(flipView.SelectedIndex);
        }
        #endregion (flipMap_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region indicator_Tapped
        //-------------------------------------------------------------------------------
        //
        private void indicator_Tapped(object sender, TappedRoutedEventArgs e)
        {
            int index = (sender == grid1_E123) ? 0
                      : (sender == grid1_E456) ? 1
                      : (sender == grid1_W) ? 2
                      : (sender == grid2_E123) ? 3
                      : (sender == grid2_E456) ? 4
                      : (sender == grid2_W) ? 5
                      : (sender == grid3_E123) ? 6
                      : (sender == grid3_E456) ? 7
                      : (sender == grid3_W) ? 8
                      : -1;

            flipMap.SelectedIndex = index;
        }
        #endregion (indicator_Tapped)
        //-------------------------------------------------------------------------------
        #region -(class)IndicatorColorController
        //-------------------------------------------------------------------------------
        private class IndicatorColorController : INotifyPropertyChanged
        {
            private Brush _greenBrush = new SolidColorBrush(Colors.LightGreen);
            private Brush _greyBrush = new SolidColorBrush(Colors.Gray);

            private int _selectedIndex = -1;

            private Brush getBrush(int index) { return (index == _selectedIndex) ? _greenBrush : _greyBrush; }

            public Brush Brush1_E123 { get { return getBrush(0); } }
            public Brush Brush1_E456 { get { return getBrush(1); } }
            public Brush Brush1_W { get { return getBrush(2); } }

            public Brush Brush2_E123 { get { return getBrush(3); } }
            public Brush Brush2_E456 { get { return getBrush(4); } }
            public Brush Brush2_W { get { return getBrush(5); } }

            public Brush Brush3_E123 { get { return getBrush(6); } }
            public Brush Brush3_E456 { get { return getBrush(7); } }
            public Brush Brush3_W { get { return getBrush(8); } }

            public void setIndex(int newIndex)
            {
                _selectedIndex = newIndex;
                OnPropertyChanged(new PropertyChangedEventArgs("Brush1_E123"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush1_E456"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush1_W"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush2_E123"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush2_E456"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush2_W"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush3_E123"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush3_E456"));
                OnPropertyChanged(new PropertyChangedEventArgs("Brush3_W"));
            }

            protected void OnPropertyChanged(PropertyChangedEventArgs e)
            {
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(e.PropertyName));
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
        //-------------------------------------------------------------------------------
        #endregion ((clas)IndicatorColorController )

        //-------------------------------------------------------------------------------
        #region -GetCanvasIndex
        //-------------------------------------------------------------------------------
        //
        private async Task<int> GetCanvasIndex(DispCircleInfo item)
        {
            string area = await _catalogData.GetBigArea(item.Block);

            int canvasIndex = (item.Day == 1 && area == _mapName1) ? 0 :
                              (item.Day == 1 && area == _mapName2) ? 1 :
                              (item.Day == 1 && area == _mapName3) ? 2 :
                              (item.Day == 2 && area == _mapName1) ? 3 :
                              (item.Day == 2 && area == _mapName2) ? 4 :
                              (item.Day == 2 && area == _mapName3) ? 5 :
                              (item.Day == 3 && area == _mapName1) ? 6 :
                              (item.Day == 3 && area == _mapName2) ? 7 :
                              (item.Day == 3 && area == _mapName3) ? 8 :
                                                                   -1;
            return canvasIndex;
        }
        #endregion (GetCanvasIndex)

        //-------------------------------------------------------------------------------
        #region listItems_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void listItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool suspendMove = _suspendMove;
            ListView listview = sender as ListView;
            if (listview == null) { return; }

            var item = listview.SelectedValue as DispCircleInfo;
            if (item == null) { return; }


            var cc = await _catalogData.GetComiketInfo();
            var rect = ComiketUtil.GetCircleRect(item.MapX, item.MapY, cc.mapSizeW, cc.mapSizeH, item.Layout, item.SpaceNoSub, await _catalogData.GetAreaChar(item.Block));

            int canvasIndex = await GetCanvasIndex(item);
            if (canvasIndex != flipMap.SelectedIndex) { flipMap.SelectedIndex = canvasIndex; }
            this.defaultViewModel["SelectBorderVisible1_E123"] = (canvasIndex == 0) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible1_E456"] = (canvasIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible1_W"] = (canvasIndex == 2) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible2_E123"] = (canvasIndex == 3) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible2_E456"] = (canvasIndex == 4) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible2_W"] = (canvasIndex == 5) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible3_E123"] = (canvasIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible3_E456"] = (canvasIndex == 7) ? Visibility.Visible : Visibility.Collapsed;
            this.defaultViewModel["SelectBorderVisible3_W"] = (canvasIndex == 8) ? Visibility.Visible : Visibility.Collapsed;

            const int RADIUS = 10;
            double l, t, w, h;
            this.defaultViewModel["SelectBorder_Left"] = l = rect.Left - RADIUS;
            this.defaultViewModel["SelectBorder_Top"] = t = rect.Top - RADIUS;
            this.defaultViewModel["SelectBorder_Width"] = w = rect.Width + RADIUS * 2;
            this.defaultViewModel["SelectBorder_Height"] = h = rect.Height + RADIUS * 2;

            //await ShowToolTipToRectangle(_serialNoToRect[selected.SerialNo]);

            if (!suspendMove) {
                ScrollViewer scViewer = SCROLLVIEWERS[canvasIndex];
                Border border = SELECT_BORDERS[canvasIndex];

                //float shift = -100 / scViewer.ZoomFactor;
                float shift = 0;

                //var gt = border.TransformToVisual(scViewer);
                //var pt = gt.TransformPoint(new Point(0, 0));
                var pt = new Point(l, t);
                scViewer.ChangeView(pt.X + shift, pt.Y + shift, null);
            }
        }
        #endregion (listItems_SelectionChanged)

        //-------------------------------------------------------------------------------
        #region toggleSwitchGenre_Toggled
        //-------------------------------------------------------------------------------
        //
        private void toggleSwitchGenre_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch tswitch = sender as ToggleSwitch;
            if (tswitch == null) { return; }
            Visibility visiblity = (tswitch.IsOn) ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;

            imgGenre1Map1_E123.Visibility = imgGenre2Map1_E123.Visibility =
            imgGenre1Map1_E456.Visibility = imgGenre2Map1_E456.Visibility =
            imgGenre1Map1_W.Visibility = imgGenre2Map1_W.Visibility =
            imgGenre1Map2_E123.Visibility = imgGenre2Map2_E123.Visibility =
            imgGenre1Map2_E456.Visibility = imgGenre2Map2_E456.Visibility =
            imgGenre1Map2_W.Visibility = imgGenre2Map2_W.Visibility =
            imgGenre1Map3_E123.Visibility = imgGenre2Map3_E123.Visibility =
            imgGenre1Map3_E456.Visibility = imgGenre2Map3_E456.Visibility =
            imgGenre1Map3_W.Visibility = imgGenre2Map3_W.Visibility =
            visiblity;
        }
        #endregion (toggleSwitchGenre_Toggled)

        //-------------------------------------------------------------------------------
        #region myHyperLink_Tapped ハイパーリンクテキストボックスクリック時
        //-------------------------------------------------------------------------------
        //
        private void myHyperLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var textblock = sender as TextBlock;
            if (textblock == null) { return; }

            this.defaultViewModel["HyperLinkURL"] = textblock.Text;
            flyout_webview.ShowAt(this);
            webView.Navigate(new Uri(textblock.Text));
        }
        #endregion (myHyperLink_Tapped)
        //-------------------------------------------------------------------------------
        #region webView_NavigationStarting
        //-------------------------------------------------------------------------------
        //
        private void webView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            processWeb.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }
        #endregion (webView_NavigationStarting)
        //-------------------------------------------------------------------------------
        #region webView_NavigationCompleted
        //-------------------------------------------------------------------------------
        //
        private void webView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            processWeb.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }
        #endregion (webView_NavigationCompleted)
    }

    //-------------------------------------------------------------------------------
    #region DispCircleInfo
    //-------------------------------------------------------------------------------
    public class DispCircleInfo
    {
        private Brush _transparentBrush = new SolidColorBrush(Colors.Transparent);
        private Brush _redBrush = new SolidColorBrush(Colors.Red);

        public DispCircleInfo()
        {
            ColorIndex = -1;
            IndexToColorFunc = null;
        }

        public int Day { get; set; }
        public char Block { get; set; }
        public char Area { get; set; }

        public int SpaceNo { get; set; }
        public int SpaceNoSub { get; set; }

        public string CircleName { get; set; }
        public string CircleKana { get; set; }
        public string PenName { get; set; }
        public string BookName { get; set; }
        public string GenreStr { get; set; }
        public string URL { get; set; }
        public string Mail { get; set; }
        public string Description { get; set; }

        public string Memo { get; set; }

        public int SerialNo { get; set; }

        public int MapX { get; set; }
        public int MapY { get; set; }
        public int Layout { get; set; }

        /// <summary>only for checklist item</summary>
        public int ColorIndex { get; set; }
        /// <summary>only for checklist item</summary>
        public Func<int, Color> IndexToColorFunc { get; set; }

        public Brush BarColor { get { return (IndexToColorFunc != null) ? new SolidColorBrush(IndexToColorFunc(ColorIndex)) : _transparentBrush; } }
        public Brush MarkColor { get { return (IndexToColorFunc != null) ? new SolidColorBrush(IndexToColorFunc(ColorIndex)) : _redBrush; } }
        public string CircleInfo { get { return string.Format("{0} {1}{2:D2}{3} {4}", Area, Block, SpaceNo, (SpaceNoSub == 0) ? 'a' : 'b', CircleName); } }
        public string SpaceStr { get { return string.Format("{0} {1}{2:D2}{3}", Area, Block, SpaceNo, (SpaceNoSub == 0) ? 'a' : 'b'); } }
    }
    //-------------------------------------------------------------------------------
    #endregion (DispCircleInfo)
}
