using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace ComicStarViewer
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage_old : Page
    {
        public MainPage_old()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// このページがフレームに表示されるときに呼び出されます。
        /// </summary>
        /// <param name="e">このページにどのように到達したかを説明するイベント データ。Parameter 
        /// プロパティは、通常、ページを構成するために使用します。</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private async void test_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            
            if (folder != null) {
                CatalogData_old cdata = new CatalogData_old();
            }
            
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

        private void listThumb_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }
    }
}
