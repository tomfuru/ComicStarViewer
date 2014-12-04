using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;


namespace ComiketCatalogBrowser_Win8 {
    public class CatalogData_old {

        private const string DATA_FOLDER_NAME = "Data";
        private const string IMAGES_FOLDER_NAME = "images";

        //-------------------------------------------------------------------------------
        #region +[static]checkExistFiles ローカルにファイルが存在しているか確認
        //-------------------------------------------------------------------------------
        //
        public static async Task<bool> checkExistFiles() {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            try {
                var data = await localDir.GetFolderAsync(DATA_FOLDER_NAME);
                var images = await localDir.GetFolderAsync(IMAGES_FOLDER_NAME);
            }
            catch (Exception e) {
                return false;
            }

            return true;
        }
        #endregion (+[static]checkExistFiles)

        //-------------------------------------------------------------------------------
        #region +[static]copyToLocal 選択フォルダからローカルへコピー
        //-------------------------------------------------------------------------------
        //
        public async static void copyToLocal(StorageFolder rootDir, Action<int, int, int, int> progressReportFunc = null) {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var dataDir_dst = await localDir.CreateFolderAsync(DATA_FOLDER_NAME, CreationCollisionOption.OpenIfExists);
            var dataDir_src = await rootDir.GetFolderAsync("CDATA");

            var fileList = await dataDir_src.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.DefaultQuery);

            if (progressReportFunc != null) { progressReportFunc(0, 2, 0, fileList.Count); }

            int count = 0;
            foreach (var file in fileList) {
                await file.CopyAsync(dataDir_dst, file.Name, NameCollisionOption.ReplaceExisting);
                count++;
                if (progressReportFunc != null) { progressReportFunc(0, 2, count, fileList.Count); }
            }

            var imageDir = await localDir.CreateFolderAsync(IMAGES_FOLDER_NAME, CreationCollisionOption.OpenIfExists);

            var imgFile = await rootDir.GetFileAsync("C084CUTL.CCZ");
            MemoryStream ms = new MemoryStream();
            using (Stream stream = (await imgFile.OpenReadAsync()).AsStream()) {
                byte[] buf = new byte[0x10000000];
                while (true) {
                    int r = stream.Read(buf, 0, buf.Length);
                    if (r <= 0) { break; }
                    ms.Write(buf, 0, r);
                }
            }
            var zipArchive = new ZipArchive(ms);

            if (progressReportFunc != null) { progressReportFunc(1, 2, 0, zipArchive.Entries.Count); }
            count = 0;

            foreach (var entry in zipArchive.Entries) {
                string name = entry.Name;
                using (Stream dstStr = await imageDir.OpenStreamForWriteAsync(name, CreationCollisionOption.ReplaceExisting))
                using (Stream srcStr = entry.Open()) {
                    int size;
                    const int BUF_SIZE = 0x100000;
                    byte[] buf = new byte[BUF_SIZE];
                    size = srcStr.Read(buf, 0, BUF_SIZE);
                    while (size > 0) {
                        dstStr.Write(buf, 0, size);
                        size = srcStr.Read(buf, 0, BUF_SIZE);
                    }
                    count++;
                    if (progressReportFunc != null) { progressReportFunc(1, 2, count, zipArchive.Entries.Count); }
                }
            }
            if (progressReportFunc != null) { progressReportFunc(2, 2, count, zipArchive.Entries.Count); }

            ms.Dispose();
            zipArchive.Dispose();
        }
        #endregion (+[static]copyToLocal)

        public bool Initialized { get; private set; }
        public BaseInfo BaseInfo { get; private set; }
        public Dictionary<int, List<CircleInfo>> CircleInfoDic = new Dictionary<int, List<CircleInfo>>();

        public CatalogData_old() {
            Initialized = false;
        }


        //-------------------------------------------------------------------------------
        #region +readLocalFiles ローカルから基本ファイルを読む
        //-------------------------------------------------------------------------------
        //
        public async Task readLocalFiles() {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var dataDir = await localDir.GetFolderAsync(DATA_FOLDER_NAME);

            var defFile = await dataDir.GetFileAsync("C84DEF.TXT");
            var bInfo = readDefFile(defFile);

            var romFile1 = await dataDir.GetFileAsync("C84ROM1.TXT");
            var circleInfo1 = readCircleInfo(romFile1, 0);
            var romFile2 = await dataDir.GetFileAsync("C84ROM2.TXT");
            var circleInfo2 = readCircleInfo(romFile2, 1);
            var romFile3 = await dataDir.GetFileAsync("C84ROM3.TXT");
            var circleInfo3 = readCircleInfo(romFile3, 2);

            this.BaseInfo = await bInfo;
            this.CircleInfoDic.Add(0, await circleInfo1);
            this.CircleInfoDic.Add(1, await circleInfo2);
            this.CircleInfoDic.Add(2, await circleInfo3);

            Initialized = true;
        }
        #endregion (+readLocalFiles)

        //-------------------------------------------------------------------------------
        #region -readDefFile DEFファイル読み込み
        //-------------------------------------------------------------------------------
        //
        private static async Task<BaseInfo> readDefFile(StorageFile defFile) {
            BaseInfo info = new BaseInfo();

            try {
                using (Stream st = (await defFile.OpenReadAsync()).AsStream())
                using (TextReader reader = new StreamReader(st, Encoding.GetEncoding("shift_jis"))) {
                    string line = reader.ReadLine();
                    string curr = "";
                    while (line != null) {
                        if (line.Length == 0 || line.StartsWith("#")) { }
                        else if (line.StartsWith("*")) {
                            // tag start
                            curr = line.Substring(1);
                        }
                        else {
                            // data start
                            if (curr.Equals("Comiket")) {
                                string[] data = line.Split('\t');
                                info.No = int.Parse(data[0]);
                                info.EventName = data[1];
                            }
                            else if (curr.Equals("cutInfo")) {
                                string[] data = line.Split('\t');

                                info.CircleCutInfo.Rect.Width = int.Parse(data[0]);
                                info.CircleCutInfo.Rect.Height = int.Parse(data[1]);
                                info.CircleCutInfo.Rect.X = int.Parse(data[2]);
                                info.CircleCutInfo.Rect.Y = int.Parse(data[3]);
                                info.CircleCutInfo.OffsetX = int.Parse(data[4]);
                                info.CircleCutInfo.OffsetY = int.Parse(data[5]);
                            }
                            else if (curr.Equals("mapTableInfo")) {
                                string[] data = line.Split('\t');

                                info.MapTableInfo.Width = int.Parse(data[0]);
                                info.MapTableInfo.Height = int.Parse(data[1]);
                                info.MapTableInfo.X = int.Parse(data[2]);
                                info.MapTableInfo.Y = int.Parse(data[3]);
                            }
                            else if (curr.Equals("ComiketDate")) {
                                string[] data = line.Split('\t');

                                DateTime dt = new DateTime(int.Parse(data[0]), int.Parse(data[1]), int.Parse(data[2]));
                                info.DateInfo.Add(Tuple.Create(dt, int.Parse(data[4])));
                            }
                            else if (curr.Equals("ComiketMap")) {
                                string[] data = line.Split('\t');

                                MapInfo mInfo = new MapInfo();

                                mInfo.MapName = data[0];
                                mInfo.MapFileWord = data[1];
                                mInfo.PrintRange.X = int.Parse(data[2]);
                                mInfo.PrintRange.Y = int.Parse(data[3]);
                                mInfo.PrintRange.Width = int.Parse(data[4]);
                                mInfo.PrintRange.Height = int.Parse(data[5]);
                                mInfo.LightMapFileWord = data[6];
                                mInfo.HighResoPrintRange.X = int.Parse(data[7]);
                                mInfo.HighResoPrintRange.Y = int.Parse(data[8]);
                                mInfo.HighResoPrintRange.Width = int.Parse(data[9]);
                                mInfo.HighResoPrintRange.Height = int.Parse(data[10]);
                                mInfo.RayoutVerticalMirrorFlag = (int.Parse(data[11]) == 1);

                                info.MapInfo.Add(mInfo);
                            }
                            else if (curr.Equals("ComiketArea")) {
                                string[] data = line.Split('\t');

                                AreaInfo aInfo = new AreaInfo();

                                aInfo.AreaName = data[0];
                                aInfo.MapName = data[1];
                                aInfo.BlockNames = data[2];
                                aInfo.PrintRange.X = int.Parse(data[3]);
                                aInfo.PrintRange.Y = int.Parse(data[4]);
                                aInfo.PrintRange.Width = int.Parse(data[5]);
                                aInfo.PrintRange.Height = int.Parse(data[6]);

                                if (data.Length > 7) {
                                    aInfo.LightMapFileWord = data[7];
                                    aInfo.HighResoPrintRange.X = int.Parse(data[8]);
                                    aInfo.HighResoPrintRange.Y = int.Parse(data[9]);
                                    aInfo.HighResoPrintRange.Width = int.Parse(data[10]);
                                    aInfo.HighResoPrintRange.Height = int.Parse(data[11]);
                                }

                                info.AreaInfo.Add(aInfo);
                            }
                            else if (curr.Equals("ComiketGenre")) {
                                string[] data = line.Split('\t');

                                info.GenreInfo.Add(Tuple.Create(int.Parse(data[0]), data[1]));
                            }
                        }
                        line = reader.ReadLine();
                    }
                }
            }
            catch (Exception e) {

            }

            return info;
        }
        #endregion (-readDefFile)
        //-------------------------------------------------------------------------------
        #region -readCircleInfo ROMnファイル読み込み
        //-------------------------------------------------------------------------------
        //
        private async static Task<List<CircleInfo>> readCircleInfo(StorageFile circleFile, int dayIndex) {
            var cList = new List<CircleInfo>();
            try {
                using (Stream st = (await circleFile.OpenReadAsync()).AsStream())
                using (TextReader reader = new StreamReader(st, Encoding.GetEncoding("shift_jis"))) {
                    string line = reader.ReadLine();
                    string curr = "";
                    while (line != null) {
                        string[] data = line.Split('\t');

                        CircleInfo cinfo = new CircleInfo() {
                            DayIndex = dayIndex,
                            MapPoint = new Point() { X = int.Parse(data[0]), Y = int.Parse(data[1]) },
                            PageNO = int.Parse(data[2]),
                            CutIndex = int.Parse(data[3]),
                            Area = data[5],
                            Block = data[6][0],
                            SpaceNo = int.Parse(data[7]),
                            GenreCode = int.Parse(data[8]),
                            CircleName = data[9],
                            CircleKana = data[10],
                            AuthorName = data[11],
                            BookName = data[12],
                            Url = data[13],
                            MailAdress = data[14],
                            OtherDescription = data[15]
                        };

                        cList.Add(cinfo);

                        line = reader.ReadLine();
                    }
                }
            }
            catch (Exception e) { }

            return cList;
        }
    }
        #endregion (-readCircleInfo)
}