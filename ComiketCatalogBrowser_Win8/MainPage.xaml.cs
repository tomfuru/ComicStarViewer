using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// 基本ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234237 を参照してください

namespace ComicStarViewer {
    /// <summary>
    /// 多くのアプリケーションに共通の特性を指定する基本ページ。
    /// </summary>
    public sealed partial class MainPage : ComicStarViewer.Common.LayoutAwarePage {
        public MainPage() {
            this.InitializeComponent();
        }

        private CatalogData _catalogData = new CatalogData();
        private bool _suspendUpdateGridView = false;

        ///-------------------------------------------------------------------------------
        #region LoadState
        //-------------------------------------------------------------------------------
        //
        // <summary>
        /// このページには、移動中に渡されるコンテンツを設定します。前のセッションからページを
        /// 再作成する場合は、保存状態も指定されます。
        /// </summary>
        /// <param name="navigationParameter">このページが最初に要求されたときに
        /// <see cref="Frame.Navigate(Type, Object)"/> に渡されたパラメーター値。
        /// </param>
        /// <param name="pageState">前のセッションでこのページによって保存された状態の
        /// ディクショナリ。ページに初めてアクセスするとき、状態は null になります。</param>
        protected async override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState) {

            if (await CatalogData.checkExistFiles()) {
                await InitializeGUI();
            }
            else {
                popup_initialize.IsOpen = true;
                story_popupAuth.Begin();
            }

            /*
            List<ThumbInfo> thumbList = new List<ThumbInfo>();
            thumbList.Add(new ThumbInfo() { Image = null, Text = "1" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "2" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "3" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "4" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "5" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "6" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "7" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "8" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "9" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "10" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "11" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "12" });
            thumbList.Add(new ThumbInfo() { Image = null, Text = "13" });

            this.DefaultViewModel["Items"] = thumbList;
            */
        }
        #endregion (LoadState)

        /// <summary>
        /// アプリケーションが中断される場合、またはページがナビゲーション キャッシュから破棄される場合、
        /// このページに関連付けられた状態を保存します。値は、
        /// <see cref="SuspensionManager.SessionState"/> のシリアル化の要件に準拠する必要があります。
        /// </summary>
        /// <param name="pageState">シリアル化可能な状態で作成される空のディクショナリ。</param>
        protected override void SaveState(Dictionary<String, Object> pageState) {
        }

        //-------------------------------------------------------------------------------
        #region selectButton_Click
        //-------------------------------------------------------------------------------
        //
        private async void selectButton_Click(object sender, RoutedEventArgs e) {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();

            buttonSelect.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            textProgress.Visibility = Windows.UI.Xaml.Visibility.Visible;
            progressInitialize.Visibility = Windows.UI.Xaml.Visibility.Visible;

            Action<int, int, int, int> update = (curr1, all1, curr2, all2) => {
                textProgress.Text = string.Format("タスク {0} / {1} , ファイル {2} / {3}", curr1, all1, curr2, all2);
                progressInitialize.Value = (double)curr2 / (double)all2;
            };

            if (folder != null) {
                await CatalogData.copyToLocal(folder, update);
                //catalogData = new CatalogData(folder);
            }

            popup_initialize.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            await InitializeGUI();
        }
        #endregion (selectButton_Click)

        //-------------------------------------------------------------------------------
        #region comboDayOfWeek_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void comboDayOfWeek_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ComboBox cmb = sender as ComboBox;
            Debug.Assert(cmb != null);
            int index = cmb.Items.IndexOf(e.AddedItems[0]);

        }
        #endregion (comboDayOfWeek_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboArea_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void comboArea_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ComboBox cmb = sender as ComboBox;
            Debug.Assert(cmb != null);
            int index = cmb.Items.IndexOf(e.AddedItems[0]);

        }

        #endregion (comboArea_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboBlock_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void comboBlock_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ComboBox cmb = sender as ComboBox;
            Debug.Assert(cmb != null);
            int index = cmb.Items.IndexOf(e.AddedItems[0]);

        }
        #endregion (comboBlock_SelectionChanged)
        //-------------------------------------------------------------------------------
        #region comboGenre_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void comboGenre_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ComboBox cmb = sender as ComboBox;
            Debug.Assert(cmb != null);
            int index = cmb.Items.IndexOf(e.AddedItems[0]);

        }
        #endregion (comboGenre_SelectionChanged)

        //-------------------------------------------------------------------------------
        #region listThumb_SelectionChanged
        //-------------------------------------------------------------------------------
        //
        private void listThumb_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }
        #endregion (listThumb_SelectionChanged)

        //-------------------------------------------------------------------------------
        #region InitializeGUI 初期化
        //-------------------------------------------------------------------------------
        //
        private async Task InitializeGUI() {
            // comboboxの初期化
            //comboDayOfWeek.DataContext = _catalogData.BaseInfo.DateInfo.Select(t => t.Item1.DayOfWeek).ToArray();
            //comboArea.DataContext = _catalogData.BaseInfo.AreaInfo.Select(ai => ai.AreaName).ToArray();
            //comboBlock.DataContext = _catalogData.BaseInfo.AreaInfo.SelectMany(ai => ai.BlockNames.ToCharArray());
            //comboGenre.DataContext = _catalogData.BaseInfo.GenreInfo.Select(t => t.Item2).ToArray();

            comboDayOfWeek.DataContext = await _catalogData.GetDays();
            comboArea.DataContext = await _catalogData.GetAreas();
            comboBlock.DataContext = await _catalogData.GetBlocks();
            comboGenre.DataContext = await _catalogData.GetGenres();

            comboDayOfWeek.IsEnabled = comboArea.IsEnabled = comboBlock.IsEnabled = comboGenre.IsEnabled = true;

            _suspendUpdateGridView = true;
            comboDayOfWeek.SelectedIndex = 0;
            comboArea.SelectedIndex = 0;
            comboBlock.SelectedIndex = 0;

            SetGridView();
            _suspendUpdateGridView = false;
        }
        #endregion


        //-------------------------------------------------------------------------------
        #region -SetGridView
        //-------------------------------------------------------------------------------
        //
        private void SetGridView() {
            int dayIndex = comboDayOfWeek.SelectedIndex;
            char block = (char)comboBlock.SelectedValue;

            //var a = _catalogData.CircleInfoDic[dayIndex].Where(ci => ci.Block == block).Select(ci => new ThumbDispData(ci.PlaceNo.ToString(), ci.getImgFileName()));
            //a.ToString();
        }
        #endregion (SetGridView)

        private async void test_Click(object sender, RoutedEventArgs e) {
            

            /*
            FileOpenPicker picker_db = new FileOpenPicker();
            picker_db.FileTypeFilter.Add(".db");
            picker_db.FileTypeFilter.Add("*");
            var file_db = await picker_db.PickSingleFileAsync();

            string local_dbfilename = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, file_db.Name);
            var local_files = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFilesAsync();
            if (!local_files.Any(sf => sf.Name.Equals(file_db.Name))) {
                await file_db.CopyAsync(Windows.Storage.ApplicationData.Current.LocalFolder, file_db.Name);
            }

            FileOpenPicker picker_img = new FileOpenPicker();
            picker_img.FileTypeFilter.Add(".CCZ");
            picker_img.FileTypeFilter.Add("*");
            var file_img = await picker_img.PickSingleFileAsync();

            CatalogData cdata = new CatalogData(local_dbfilename, file_img);
            */

            /*
            FolderPicker foPicker = new FolderPicker();
            foPicker.FileTypeFilter.Add("*");
            var folder = await foPicker.PickSingleFolderAsync();
            var file = await folder.GetFileAsync("C084CUTL.CCZ");
            file.ToString();
             * */
        }

        private void Button_Click(object sender, RoutedEventArgs e) {

        }

        
    }
}

class ThumbDispData {
    public BitmapImage Image { get; private set; }
    public string Text { get; private set; }
    private string _imgPath;

    public ThumbDispData(string text, string imgPath) {
        Text = text;
        Image = null;
        _imgPath = imgPath;

        //Task.Run(() => getImage());
    }

    public void getImage() {

    }
}