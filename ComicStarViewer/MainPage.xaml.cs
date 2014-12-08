using ComicStarViewer.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// 基本ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234237 を参照してください

namespace ComicStarViewer
{
    /// <summary>
    /// 多くのアプリケーションに共通の特性を指定する基本ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // 表示データ取得・設定
        private MyModelView _myModelView = new MyModelView();
        // ブックマーク
        private Bookmarks _bookmark = new Bookmarks();
        // GUI関係
        private bool _suspendGridViewUpdate = false;
        // ロード時に使用
        private bool _loadSession = false;
        private int _initSelectedIndex;
        private int _initDayIndex;

        //-------------------------------------------------------------------------------
        #region Util
        //-------------------------------------------------------------------------------
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        /// <summary>
        /// NavigationHelper は、ナビゲーションおよびプロセス継続時間管理を
        /// 支援するために、各ページで使用します。
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// これは厳密に型指定されたビュー モデルに変更できます。
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }
        //-------------------------------------------------------------------------------
        #endregion (Util)

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public MainPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;
        }
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region navigationHelper_LoadState
        //-------------------------------------------------------------------------------
        /// <summary>
        /// このページには、移動中に渡されるコンテンツを設定します。前のセッションからページを
        /// 再作成する場合は、保存状態も指定されます。
        /// </summary>
        /// <param name="sender">
        /// イベントのソース (通常、<see cref="NavigationHelper"/>)
        /// </param>
        /// <param name="e">このページが最初に要求されたときに
        /// <see cref="Frame.Navigate(Type, Object)"/> に渡されたナビゲーション パラメーターと、
        /// 前のセッションでこのページによって保存された状態の辞書を提供する
        /// イベント データ。ページに初めてアクセスするとき、状態は null になります。</param>
        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            if (e.PageState != null) {
                try {
                    _loadSession = true;
                    _initSelectedIndex = (int)e.PageState["SelectedIndex"];
                    _initDayIndex = (int)e.PageState["SelectedDayIndex"];
                }
                catch (Exception) {
                    _loadSession = false;
                }
            }
        }
        //-------------------------------------------------------------------------------
        #endregion (navigationHelper_LoadState)
        //-------------------------------------------------------------------------------
        #region navigationHelper_SaveState
        //-------------------------------------------------------------------------------
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
            if (comboDayOfWeek.SelectedIndex >= 0 && gridThumb.SelectedIndex >= 0) {
                e.PageState["SelectedIndex"] = gridThumb.SelectedIndex;
                e.PageState["SelectedDayIndex"] = comboDayOfWeek.SelectedIndex;
            }

        }
        //-------------------------------------------------------------------------------
        #endregion (navigationHelper_SaveState)

        //-------------------------------------------------------------------------------
        #region NavigationHelper の登録 (OnNavigatedTo / OnNavigatedFrom)

        /// このセクションに示したメソッドは、NavigationHelper がページの
        /// ナビゲーション メソッドに応答できるようにするためにのみ使用します。
        /// 
        /// ページ固有のロジックは、
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// および <see cref="GridCS.Common.NavigationHelper.SaveState"/> のイベント ハンドラーに配置する必要があります。
        /// LoadState メソッドでは、前のセッションで保存されたページの状態に加え、
        /// ナビゲーション パラメーターを使用できます。

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedTo(e);

            this.defaultViewModel["Data"] = _myModelView;
            this.defaultViewModel["Bookmark"] = _bookmark;

            if (await CatalogData.checkExistFiles()) {
                bool opened = await _myModelView.Initialize();
                if (opened) {
                    // ブックマーク初期化
                    var cinfo = await _myModelView.CatalogData.GetComiketInfo();
                    await _bookmark.Initialize(cinfo.comiketNo);
                    // GUI初期化
                    if (_loadSession) {
                        // 以前セッションデータ存在時
                        _myModelView.GetItems(_initSelectedIndex);
                        _suspendGridViewUpdate = true;
                        comboDayOfWeek.SelectedIndex = _initDayIndex;
                        await _myModelView.UpdateBlockComboBox(_initDayIndex + 1);
                        _suspendGridViewUpdate = false;
                        gridThumb.SelectedIndex = _initSelectedIndex;
                        gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
                    }
                    else {
                        // 以前セッションデータ無しの時
                        comboDayOfWeek.SelectedIndex = 0; // raise selection_changed event
                    }

                    comboDayOfWeek.IsEnabled = comboArea.IsEnabled = comboBlock.IsEnabled = comboGenre.IsEnabled = true;
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }
        #endregion

        // Menu Items
        // (Top menu)
        //-------------------------------------------------------------------------------
        #region searchButton_Click [TopAppBar - search]
        //-------------------------------------------------------------------------------
        //
        private async void searchButton_Click(object sender, RoutedEventArgs e)
        {
            await _myModelView.CheckList.SaveCheckListToLocal();
            this.Frame.Navigate(typeof(ListPage));
            //this.Frame.Navigate(typeof(ListPage), Tuple.Create(_myModelView.CatalogData, _myModelView.CheckList));
        }
        #endregion (searchButton_Click)
        //-------------------------------------------------------------------------------
        #region bookmarkButton_Click [TopAppBar - bookmark]
        //-------------------------------------------------------------------------------
        //
        private void bookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement elem = sender as FrameworkElement;
            flyoutBookmarks.ShowAt(elem);
        }
        #endregion (bookmarkButton_Click)
        // (Bottom menu)
        //-------------------------------------------------------------------------------
        #region importDataButton_Click [BottomAppBar - import Data]
        //-------------------------------------------------------------------------------
        //
        private void importDataButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CopyPage));
        }
        #endregion (importDataButton_Click)
        //-------------------------------------------------------------------------------
        #region importCheckList_Click [BottomAppBar - import CheckList]
        //-------------------------------------------------------------------------------
        //
        private async void importCheckList_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();

            if (file == null) { return; }

            bool res = await _myModelView.OpenCheckList(file);
            if (res) {
                await CheckList.CopyToLocal(file);
                flyout_completeMessage.ShowAt(this);
            }
            else {
                flyout_failedMessage.ShowAt(this);
            }
        }
        #endregion (importCheckList_Click)
        //-------------------------------------------------------------------------------
        #region saveCheckListAs_Click [BottomAppBar - save CheckListAs]
        //-------------------------------------------------------------------------------
        //
        private async void saveCheckListAs_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker fsp = new FileSavePicker();
            fsp.DefaultFileExtension = ".csv";
            fsp.FileTypeChoices.Add("CSVファイル", new List<string> { ".csv" });
            StorageFile file = await fsp.PickSaveFileAsync();

            if (file == null) { return; }
            await _myModelView.CheckList.SaveCheckList(file);
            flyout_completeMessage.ShowAt(this);
        }
        #endregion (saveCheckList_Click)
        //-------------------------------------------------------------------------------
        #region clearCheckList_Click [BottomAppBar - clear CheckList]
        //-------------------------------------------------------------------------------
        //
        private async void clearCheckList_Click(object sender, RoutedEventArgs e)
        {
            var ok = await IsOKDialog("本当にチェックリストをクリアして宜しいですか？");
            if (ok) {
                _myModelView.CheckList.ClearCircles();
                flyout_completeMessage.ShowAt(this);
                await _myModelView.CheckList.SaveCheckListToLocal();
            }
        }
        #endregion (clearCheckList_Click)

        // Combo boxes for display contents
        //-------------------------------------------------------------------------------
        #region comboDayOfWeek_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void comboDayOfWeek_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(comboDayOfWeek == sender);
            if (_suspendGridViewUpdate) { return; }
            if (comboDayOfWeek.SelectedIndex < 0) { return; }

            int dayIndex = comboDayOfWeek.Items.IndexOf(e.AddedItems[0]) + 1;

            _suspendGridViewUpdate = true;
            await _myModelView.UpdateBlockComboBox(dayIndex);
            _suspendGridViewUpdate = false;

            var index = await _myModelView.GetGridIndex(dayIndex);

            gridThumb.SelectedIndex = index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
        }
        #endregion (comboDayOfWeek_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboArea_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void comboArea_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(comboArea == sender);
            if (_suspendGridViewUpdate) { return; }
            if (comboArea.SelectedIndex < 0 || comboDayOfWeek.SelectedIndex < 0) { return; }

            //int index = comboArea.Items.IndexOf(e.AddedItems[0]);
            string area = (string)comboArea.SelectedValue;
            int dayIndex = comboDayOfWeek.SelectedIndex + 1;

            int index = await _myModelView.GetGridIndex(dayIndex, area);

            gridThumb.SelectedIndex = index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
        }

        #endregion (comboArea_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboBlock_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void comboBlock_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(comboBlock == sender);
            if (_suspendGridViewUpdate) { return; }
            if (comboBlock.SelectedIndex < 0 || comboDayOfWeek.SelectedIndex < 0) { return; }

            //int index = comboBlock.Items.IndexOf(e.AddedItems[0]);
            char block = (char)comboBlock.SelectedValue;
            int dayIndex = comboDayOfWeek.SelectedIndex + 1;

            int index = await _myModelView.GetGridIndex(dayIndex, block);

            gridThumb.SelectedIndex = index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
        }
        #endregion (comboBlock_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboGenre_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void comboGenre_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(comboGenre == sender);
            if (_suspendGridViewUpdate) { return; }
            if (comboGenre.SelectedIndex < 0) { return; }

            string genreStr = (string)comboGenre.SelectedValue;

            int index = await _myModelView.GetGridIndex(genreStr);

            gridThumb.SelectedIndex = index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);

            /*_suspendGridViewUpdate = true;
            var pos = await _myModelView.SetGridViewData(genreStr);
            if (pos == null) {
                comboDayOfWeek.SelectedIndex = -1;
                comboArea.SelectedIndex = -1;
                comboBlock.SelectedIndex = -1;
            }
            else {
                comboDayOfWeek.SelectedIndex = pos.Item1 - 1;
                comboArea.SelectedValue = pos.Item2;
                comboBlock.SelectedValue = pos.Item3;

                gridThumb.SelectedIndex = pos.Item4;
                gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
            }
            _suspendGridViewUpdate = false;*/
        }
        #endregion (comboGenre_SelectionChanged)

        // ブックマーク
        //-------------------------------------------------------------------------------
        #region bookmarkAddButton_Click ブックマーク追加ボタン
        //-------------------------------------------------------------------------------
        //
        private async void bookmarkAddButton_Click(object sender, RoutedEventArgs e)
        {
            int dayIndex = comboDayOfWeek.SelectedIndex;
            int itemIndex = gridThumb.SelectedIndex;
            string itemArea = (string)comboArea.SelectedValue;
            char itemBlock = (char)comboBlock.SelectedValue;

            bool successed = await _bookmark.Add(textBookmarkName.Text, dayIndex, itemIndex, itemArea, itemBlock);
            if (successed) {
                //flyout_completeMessage.ShowAt(this);
            }
            else {

            }
        }
        #endregion (bookmarkAddButton_Click)
        //-------------------------------------------------------------------------------
        #region bookmarkRemoveButton_Click ブックマーク削除ボタン
        //-------------------------------------------------------------------------------
        //
        private void bookmarkRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: not implemented
            throw new NotImplementedException();
            
            //Button b = sender as Button;
            //e.ToString();
        }
        #endregion (bookmarkRemoveButton_Click)
        //-------------------------------------------------------------------------------
        #region listViewBookmarks_ItemClick ブックマーク項目クリック
        //-------------------------------------------------------------------------------
        //
        private async void listViewBookmarks_ItemClick(object sender, ItemClickEventArgs e)
        {
            var bmd = e.ClickedItem as BookmarkData;

            // move to bookmark item
            _myModelView.GetItems(bmd.Index);
            _suspendGridViewUpdate = true;
            comboDayOfWeek.SelectedIndex = bmd.Day;
            await _myModelView.UpdateBlockComboBox(bmd.DayBase1);
            _suspendGridViewUpdate = false;
            gridThumb.SelectedIndex = bmd.Index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);

            flyoutBookmarks.Hide();
            appbar_top.IsOpen = appbar_bottom.IsOpen = false;
        }
        #endregion (listViewBookmarks_ItemClick)

        // メイン グリッドアイテムリスト
        //-------------------------------------------------------------------------------
        #region gridThumb_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void gridThumb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) { return; }

            ThumbDispData tdd = e.AddedItems[0] as ThumbDispData;
            if (tdd == null) { return; }

            _suspendGridViewUpdate = true;

            if (comboDayOfWeek.SelectedIndex >= 0 && comboDayOfWeek.SelectedIndex != tdd.CircleData.day - 1) {
                comboDayOfWeek.SelectedIndex = tdd.CircleData.day - 1;
                await _myModelView.UpdateBlockComboBox(tdd.CircleData.day);
            }
            comboArea.SelectedValue = await _myModelView.CatalogData.GetArea(tdd.CircleData.blockId);
            comboBlock.SelectedValue = await _myModelView.CatalogData.GetBlockChr(tdd.CircleData.blockId);
            comboGenre.SelectedValue = await _myModelView.ConvertGenreIdToGenreStr(tdd.CircleData.genreId);

            _suspendGridViewUpdate = false;

            await _myModelView.SetCircleDetail(tdd.CircleData);
        }
        #endregion (gridThumb_SelectionChanged)

        // グリッドアイテムのContext Menu(チェックリストに追加)
        //-------------------------------------------------------------------------------
        #region gridItem_RightTapped
        //-------------------------------------------------------------------------------
        //
        private void gridItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse) {
                FrameworkElement elem = sender as FrameworkElement;
                var data = elem.DataContext as ThumbDispData;
                if (gridThumb.SelectedItem != data) {
                    gridThumb.SelectedItem = data;
                }
                comboColorSelect.SelectedIndex = 0;
                flyoutCheckListColorSelect.ShowAt(elem);
                e.Handled = true;
            }
        }
        #endregion (gridItem_RightTapped)
        //-------------------------------------------------------------------------------
        #region gridItem_Holding
        //-------------------------------------------------------------------------------
        //
        private void gridItem_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement elem = sender as FrameworkElement;
            comboColorSelect.SelectedIndex = 0;
            flyoutCheckListColorSelect.ShowAt(elem);
        }
        #endregion (gridItem_Holding)
        //-------------------------------------------------------------------------------
        #region buttonAddCheckList_Click チェックリスト追加ボタン
        //-------------------------------------------------------------------------------
        //
        private async void buttonAddCheckList_Click(object sender, RoutedEventArgs e)
        {
            var color = (ColorInfo)comboColorSelect.SelectedItem;
            var circleDisp = (ThumbDispData)gridThumb.SelectedItem;

            await _myModelView.AddCircleToCheckList(circleDisp.CircleData, color.Index);

            flyoutCheckListColorSelect.Hide();
            flyout_completeMessage.ShowAt(sender as FrameworkElement);
        }
        #endregion (buttonAddCheckList_Click)

        // URL関係
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

        // チェックリストソート
        //-------------------------------------------------------------------------------
        #region comboSortType_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void comboSortType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _myModelView.UpdateSortingOfCheckList();
        }
        #endregion (comboSortType_SelectionChanged)

        // チェックリスト詳細項目内ボタン
        //-------------------------------------------------------------------------------
        #region buttonTimeRecord_Click 時間記録ボタン
        //-------------------------------------------------------------------------------
        //
        private void buttonTimeRecord_Click(object sender, RoutedEventArgs e)
        {
            DateTimeType dtt;
            if (sender == btnTimeRecord_並び始め) {
                dtt = DateTimeType.並び始め;
            }
            else if (sender == btnTimeRecord_購入完了) {
                dtt = DateTimeType.購入完了;
            }
            else { return; }

            _myModelView.UpdateDateTimeInfo(dtt);
        }
        #endregion (buttonTimeRecord_Click)
        //-------------------------------------------------------------------------------
        #region buttonUndoTime_Click 時間Undoボタン
        //-------------------------------------------------------------------------------
        //
        private void buttonUndoTime_Click(object sender, RoutedEventArgs e)
        {
            _myModelView.UndoDateTimeInfo();
        }
        #endregion (buttonUndoTime_Click)

        // チェックリストのContext Menu
        private FrameworkElement _checkListItem_menuSelected = null;
        //-------------------------------------------------------------------------------
        #region listViewCheckList_RightTapped
        //-------------------------------------------------------------------------------
        //
        private void listViewCheckList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse) {
                FrameworkElement elem = e.OriginalSource as FrameworkElement;
                _checkListItem_menuSelected = elem;
                ListView listView;

                listView = GetListCheckList(flipView_checkList.SelectedIndex);
                if (listView == null) { return; }

                if (listView.SelectedItem != elem.DataContext) {
                    listView.SelectedItem = elem.DataContext;
                }

                flyout_checkListMenu.ShowAt(elem);
                e.Handled = true;
            }
        }
        #endregion (listViewCheckList_RightTapped)
        //-------------------------------------------------------------------------------
        #region listViewCheckList_Holding
        //-------------------------------------------------------------------------------
        //
        private void listViewCheckList_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement elem = e.OriginalSource as FrameworkElement;
            _checkListItem_menuSelected = elem;
            flyout_checkListMenu.ShowAt(elem);
        }
        #endregion (listViewCheckList_Holding)
        //-------------------------------------------------------------------------------
        #region flyout_checkListmenu_Delete
        //-------------------------------------------------------------------------------
        //
        private async void flyout_checkListmenu_Delete(object sender, RoutedEventArgs e)
        {
            var ok = await IsOKDialog("本当に削除して宜しいですか？");
            if (ok) {
                await _myModelView.RemoveSelectedCheckListItem();
            }
        }
        #endregion (flyout_checkListmenu_Delete)
        //-------------------------------------------------------------------------------
        #region flyout_checkListmenu_ChangeColor
        //-------------------------------------------------------------------------------
        //
        private void flyout_checkListmenu_ChangeColor(object sender, RoutedEventArgs e)
        {
            var colorIndex = _myModelView.GetCurrentSelectedCheckListItemColorIndex();
            comboColorSelect2.SelectedIndex = colorIndex - 1;
            flyoutCheckListColorSelect2.ShowAt(_checkListItem_menuSelected);
        }
        #endregion (flyout_checkListmenu_ChangeColor)
        //-------------------------------------------------------------------------------
        #region flyout_checkListmenu_Move
        //-------------------------------------------------------------------------------
        //
        private async void flyout_checkListmenu_Move(object sender, RoutedEventArgs e)
        {
            int index = await _myModelView.GetIndexOfSelectedCheckListCircle();
            gridThumb.SelectedIndex = index;
            gridThumb.ScrollIntoView(gridThumb.SelectedValue, ScrollIntoViewAlignment.Leading);
        }
        #endregion (flyout_checkListmenu_Move)
        //-------------------------------------------------------------------------------
        #region buttonChangeColor_Click
        //-------------------------------------------------------------------------------
        //
        private async void buttonChangeColor_Click(object sender, RoutedEventArgs e)
        {
            var color = (ColorInfo)comboColorSelect2.SelectedItem;

            var listChecklist = GetListCheckList(flipView_checkList.SelectedIndex);
            if (listChecklist == null) { return; }

            await _myModelView.ChangeSelectedCheckListItemColor(color.Index);

            flyoutCheckListColorSelect.Hide();
            flyout_completeMessage.ShowAt(sender as FrameworkElement);
        }
        #endregion (buttonChangeColor_Click)
        //-------------------------------------------------------------------------------
        #region listViewCheckList_DoubleTapped チェックリストlistviewダブルタップ：詳細flyout表示
        //-------------------------------------------------------------------------------
        //
        private void listViewCheckList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ListView listview = sender as ListView;
            if (sender == null) { return; }
            var item = listview.SelectedItem as CheckFileCircleInfo;
            if (item == null) { return; }

            flyout_checklist.ShowAt(this);
        }
        #endregion (listView_DoubleTapped)
        //-------------------------------------------------------------------------------
        #region flyout_checklist_Closed 選択チェックリスト表示flyout close時
        //-------------------------------------------------------------------------------
        //
        private async void flyout_checklist_Closed(object sender, object e)
        {
            _myModelView.RefreshSelectedCheckListItem();
            await _myModelView.SaveSelectedCheckListItem();
            await _myModelView.CheckList.SaveCheckListToLocal();
        }
        #endregion (flyout_checklist_Closed)

        // チェックリストアイテム選択
        //-------------------------------------------------------------------------------
        #region flipView_checkList_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void flipView_checkList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender != flipView_checkList) { return; }
            ListView listView;
            bool known;
            switch (flipView_checkList.SelectedIndex) {
                case 0: listView = listViewCheckList_1; known = true; break;
                case 1: listView = listViewCheckList_2; known = true; break;
                case 2: listView = listViewCheckList_3; known = true; break;
                case 3: listView = listViewCheckList_Unknown; known = false; break;
                default: return;
            }

            if (known) {
                var item = listView.SelectedItem as CheckFileCircleInfo;
                await _myModelView.SetSelectedCheckListItem(item);
            }
            else {
                var item = listView.SelectedItem as CheckFileUnknownCircleInfo;
                _myModelView.SetSelectedCheckListItem(item);
            }
        }
        #endregion (flipView_checkList_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region listViewCheckList_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private async void listViewCheckList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView == null) { return; }
            switch (flipView_checkList.SelectedIndex) {
                case 0: if (listView != listViewCheckList_1) { return; }; break;
                case 1: if (listView != listViewCheckList_2) { return; }; break;
                case 2: if (listView != listViewCheckList_3) { return; }; break;
                default: return;
            }

            CheckFileCircleInfo cfci = listView.SelectedItem as CheckFileCircleInfo;
            if (cfci == null) { return; }
            await _myModelView.SetSelectedCheckListItem(cfci);
        }
        #endregion (listViewCheckList_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region listViewCheckList_Unknown_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void listViewCheckList_Unknown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView == null) { return; }
            if (flipView_checkList.SelectedIndex != 3) { return; } // 自分がflipViewで選択されていないときは選択しない

            CheckFileUnknownCircleInfo cfuci = listView.SelectedItem as CheckFileUnknownCircleInfo;
            if (cfuci == null) { return; }
            _myModelView.SetSelectedCheckListItem(cfuci);
        }
        #endregion (listViewCheckList_Unknown_SelectionChanged)


        //-------------------------------------------------------------------------------
        #region -GetListCheckList 日付->チェックリストのリスト
        //-------------------------------------------------------------------------------
        //
        private ListView GetListCheckList(int flipViewIndex)
        {
            switch (flipViewIndex) {
                case 0: return listViewCheckList_1;
                case 1: return listViewCheckList_2;
                case 2: return listViewCheckList_3;
                default: return null;
            }
        }
        #endregion (GetListCheckList)

        //-------------------------------------------------------------------------------
        #region -IsOKDialog 確認ダイアログ
        //-------------------------------------------------------------------------------
        //
        private async Task<bool> IsOKDialog(string questionMessage)
        {
            MessageDialog md = new MessageDialog(questionMessage);
            md.Options = MessageDialogOptions.AcceptUserInputAfterDelay;
            var ok = new UICommand("OK");
            var cansel = new UICommand("Cansel");
            md.Commands.Add(ok);
            md.Commands.Add(cansel);
            md.DefaultCommandIndex = 1;
            md.CancelCommandIndex = 1;

            var res = await md.ShowAsync();
            return (res.Label != cansel.Label);
        }
        #endregion (IsOKDialog)
    }

}