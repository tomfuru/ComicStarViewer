using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicStarViewer
{
    public class EntireMemo
    {
        private static string ENTIREMEMO_DEFAULT_FILENAME = "entirememo.txt";

        public string Contents { get; set; }
        //-------------------------------------------------------------------------------
        #region -[static, async] GetDefaultFile デフォルトファイルハンドル取得
        //-------------------------------------------------------------------------------
        //
        private async static Task<StorageFile> GetDefaultFile()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var files = await localDir.GetFilesAsync();
            var file = files.Where(sf => sf.Name == ENTIREMEMO_DEFAULT_FILENAME)
                            .FirstOrDefault();
            return file;
        }
        #endregion (-[static, async])

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        EntireMemo()
        {
            Contents = "";
        }
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region +[async]Open 開く
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> Open()
        {
            // デフォルトファイル取得
            var file = await EntireMemo.GetDefaultFile();

            if (file != null) {
                Contents = await FileIO.ReadTextAsync(file);
            }
            return true;
        }
        #endregion (Open)
        //-------------------------------------------------------------------------------
        #region +[async]Save 保存
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> Save()
        {
            var file = await EntireMemo.GetDefaultFile();
            await FileIO.WriteTextAsync(file, Contents);
            return true;
        }
        #endregion (Save)
    }
}
