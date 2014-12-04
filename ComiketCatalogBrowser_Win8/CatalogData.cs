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
using Windows.Storage.Search;
using SQLite;
using ComiketCatalogBrowser_Win8;
using System.Diagnostics;


namespace ComiketCatalogBrowser_Win8 {
    public class CatalogData {

        private const string REG_DB_FILE_NAME = @"^ccatalog\d+\.db$";
        private const string IMAGES_FOLDER_NAME = "images";

        private SQLiteConnection _conn = null;
        private string _dbFilePath = null;

        //-------------------------------------------------------------------------------
        #region +[static]checkExistFiles ローカルにファイルが存在しているか確認
        //-------------------------------------------------------------------------------
        //
        public static async Task<bool> checkExistFiles() {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            if (!files.Any(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME))) {
                return false;
            }

            var folders = await localDir.GetFoldersAsync();
            if (!folders.Any(sf => sf.Name == IMAGES_FOLDER_NAME)) {
                return false;
            }

            return true;
        }
        #endregion (+[static]checkExistFiles)

        //-------------------------------------------------------------------------------
        #region +[static]copyToLocal 選択フォルダからローカルへコピー
        //-------------------------------------------------------------------------------
        //
        public async static Task copyToLocal(StorageFolder rootDir, Action<int, int, int, int> progressReportFunc = null) {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            // db file
            var files = await rootDir.GetFilesAsync(CommonFileQuery.OrderByName);
            var dbfile = files.Where(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME))
                               .First();

            if (progressReportFunc != null) { progressReportFunc(0, 2, 0, 1); }
            await dbfile.CopyAsync(localDir, dbfile.Name, NameCollisionOption.ReplaceExisting);
            if (progressReportFunc != null) { progressReportFunc(0, 2, 1, 1); }

            var imageDir = await localDir.CreateFolderAsync(IMAGES_FOLDER_NAME, CreationCollisionOption.OpenIfExists);

            // image files
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
            int count = 0;

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

        public bool Connected { get; private set; }
        public BaseInfo BaseInfo { get; private set; }
        public Dictionary<int, List<CircleInfo>> CircleInfoDic = new Dictionary<int, List<CircleInfo>>();

        public CatalogData() {
            Connected = false;
        }

        //-------------------------------------------------------------------------------
        #region +CloseConnection
        //-------------------------------------------------------------------------------
        //
        public void CloseConnection() {
            if (Connected) {
                _conn.Close();
                _conn = null;
                Connected = false;
            }
        }
        #endregion (CloseConnection)

        //-------------------------------------------------------------------------------
        #region -connect 接続
        //-------------------------------------------------------------------------------
        //
        private async Task connect() {
            if (_dbFilePath == null) {
                var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
                var localFiles = await localDir.GetFilesAsync();
                var dbFile = localFiles.Where(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME))
                                       .First();

                _dbFilePath = dbFile.Path;
            }

            _conn = new SQLiteConnection(_dbFilePath);
            Connected = true;
        }
        #endregion (connect)

        //-------------------------------------------------------------------------------
        #region +GetDays 曜日一覧
        //-------------------------------------------------------------------------------
        //
        public async Task<DayOfWeek[]> GetDays() {
            if (!Connected) { await connect(); }
            var dates = DBQueries.GetDays(_conn);
            return dates.Select(cd => (new DateTime(cd.year, cd.month, cd.day)).DayOfWeek).ToArray();
        }
        #endregion (GetDays)
        //-------------------------------------------------------------------------------
        #region +GetAreas エリア一覧(東123,東1,...等)
        //-------------------------------------------------------------------------------
        //
        public async Task<string[]> GetAreas() {
            if (!Connected) { await connect(); }
            return DBQueries.GetAreaNames(_conn).ToArray();
        }
        #endregion (GetAreas)
        //-------------------------------------------------------------------------------
        #region +GetBlocks ブロック一覧(あいうABC...等)
        //-------------------------------------------------------------------------------
        //
        public async Task<char[]> GetBlocks() {
            if (!Connected) { await connect(); }
            return DBQueries.GetBlockNames(_conn).ToArray();
        }
        #endregion (+GetBlocks)
        //-------------------------------------------------------------------------------
        #region +GetGenres ジャンル一覧(文字列)
        //-------------------------------------------------------------------------------
        //
        public async Task<string[]> GetGenres() {
            if (!Connected) { await connect(); }
            return DBQueries.GetGenreNames(_conn).ToArray();
        }
        #endregion (GetGenres)
    }

    static class DBQueries {
        //-------------------------------------------------------------------------------
        #region +[static]GetComiketInfo カタログデータ取得
        //-------------------------------------------------------------------------------
        //
        public static ComiketInfo GetComiketInfo(SQLiteConnection conn) {
            var res = conn.Query<ComiketInfo>("select * from ComiketInfo");
            Debug.Assert(res.Count == 1);
            return res[0];
        }
        #endregion (GetComiketInfo)

        //-------------------------------------------------------------------------------
        #region +[static]GetDays 日一覧取得
        //-------------------------------------------------------------------------------
        //
        public static IEnumerable<ComiketDate> GetDays(SQLiteConnection conn) {
            return conn.Query<ComiketDate>("select * from ComiketDate");
        }
        #endregion (+[static]GetDays)

        //-------------------------------------------------------------------------------
        #region +[static]GetAreaNames エリア名前一覧
        //-------------------------------------------------------------------------------
        //
        public static IEnumerable<string> GetAreaNames(SQLiteConnection conn) {
            //var res = conn.Query<ComiketArea>("select * from ComiketArea");
            var res = conn.Query<ComiketArea>("select * from ComiketArea");
            return res.Select(ca => ca.name).ToList();
        }
        #endregion (GetAreaNames)

        //-------------------------------------------------------------------------------
        #region +[static]GetBlockNames ブロック名前一覧
        //-------------------------------------------------------------------------------
        //
        public static IEnumerable<char> GetBlockNames(SQLiteConnection conn) {
            var res = conn.Query<ComiketBlock>("select * from ComiketBlock");
            return res.Select(cb => cb.name[0]).ToList();
        }
        #endregion (GetBlockNames)

        //-------------------------------------------------------------------------------
        #region +[static]GetGenreNames ジャンル名前一覧
        //-------------------------------------------------------------------------------
        //
        public static IEnumerable<string> GetGenreNames(SQLiteConnection conn) {
            var res = conn.Query<ComiketGenre>("select * from ComiketGenre");
            return res.Select(cg => cg.name);
        }
        #endregion (GetGenreNames)
    }
    //-------------------------------------------------------------------------------
    #region Classes for SQLite DB
    //-------------------------------------------------------------------------------
    // class for SQLite DB
    public class ComiketInfo {
        [PrimaryKey]
        public int comiketNo { get; set; }
        [MaxLength(20)]
        public string comiketName { get; set; }
        public int cutSizeW { get; set; }
        public int cutSizeH { get; set; }
        public int cutOriginX { get; set; }
        public int cutOriginY { get; set; }
        public int cutOffsetX { get; set; }
        public int cutOffsetY { get; set; }
        public int mapSizeW { get; set; }
        public int mapSizeH { get; set; }
        public int mapOriginX { get; set; }
        public int mapOriginY { get; set; }
    }

    public class ComiketDate {
        [PrimaryKey]
        public int comiketNo { get; set; }
        [PrimaryKey]
        public int id { get; set; }
        public int year { get; set; }
        public int month { get; set; }
        public int day { get; set; }
        public int weekday { get; set; }
    }

    public class ComiketArea {
        public string name { get; set; }
        public string simpleName { get; set; }

    }

    public class ComiketBlock {
        public string name { get; set; }
        public int areaId { get; set; }
    }

    public class ComiketGenre {
        public string name { get; set; }
        public int code { get; set; }
    }
    //-------------------------------------------------------------------------------
    #endregion (Classes for SQLite DB)

    public class BaseInfo {
        public int No;
        public string EventName;
        public CircleCutInfo CircleCutInfo;
        public Rectangle MapTableInfo;
        public List<Tuple<DateTime, int>> DateInfo = new List<Tuple<DateTime, int>>();
        public List<MapInfo> MapInfo = new List<MapInfo>();
        public List<AreaInfo> AreaInfo = new List<AreaInfo>();
        public List<Tuple<int, string>> GenreInfo = new List<Tuple<int, string>>();

        public override string ToString() { return EventName; }
    }

    public struct CircleCutInfo {
        public Rectangle Rect;
        public int OffsetX;
        public int OffsetY;

        public override string ToString() {
            return string.Format("{{X = {0}, Y = {1}, Width = {2}, Height = {3} OffsetX = {4}, OffsetY = {5}}}",
                                Rect.X, Rect.Y, Rect.Width, Rect.Height, OffsetX, OffsetY);
        }
    }

    public struct MapInfo {
        public string MapName;
        public string MapFileWord;
        public Rectangle PrintRange;
        public string LightMapFileWord;
        public Rectangle HighResoPrintRange;
        public bool RayoutVerticalMirrorFlag;

        public override string ToString() { return MapName; }
    }

    public struct AreaInfo {
        public string AreaName;
        public string MapName;
        public string BlockNames;
        public Rectangle PrintRange;
        public string LightMapFileWord;
        public Rectangle HighResoPrintRange;

        public override string ToString() { return AreaName; }
    }

    public struct CircleInfo {
        public int DayIndex; // 0 or 1 or 2
        public Point MapPoint;
        public int PageNO;
        public int CutIndex;
        public string Area;
        public char Block;
        public int SpaceNo;
        public int GenreCode;
        public string CircleName;
        public string CircleKana;
        public string AuthorName;
        public string BookName;
        public string Url;
        public string MailAdress;
        public string OtherDescription;

        public int PlaceNo { get { return SpaceNo / 2 + 1; } }
        public char PlaceSign { get { return ((SpaceNo % 2) == 1 ? 'a' : 'b'); } }
        public string getImgFileName() {
            return string.Format("");
        }

        public override string ToString() { return string.Format("{0}日目 {1}{2}{3:00}{4} {5}", DayIndex + 1, Area, Block, PlaceNo, PlaceSign, CircleName); }
    }

    public struct Rectangle {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public int Left { get { return X; } }
        public int Right { get { return X + Width; } }
        public int Top { get { return Y; } }
        public int Bottom { get { return Y + Height; } }

        public override string ToString() { return string.Format("{{X = {0}, Y = {1}, Width = {2}, Height = {3}}}", X, Y, Width, Height); }
    }

    public struct Point {
        public int X;
        public int Y;

        public override string ToString() { return string.Format("{{X = {0}, Y = {1}}}", X, Y); }
    }

    public class ThumbInfo {
        public BitmapImage Image { get; set; }
        public string Text { get; set; }
    }
}