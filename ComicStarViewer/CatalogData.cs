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

using ComicStarViewer;
using System.Diagnostics;
using Windows.Storage.Streams;


namespace ComicStarViewer {
    public class CatalogData {
        public static readonly Dictionary<DayOfWeek, char> DayOfWeekToChrDic;
        public static readonly Dictionary<char, DayOfWeek> ChrToDayOfWeekDic;
        //-------------------------------------------------------------------------------
        #region (static)Constructor
        //-------------------------------------------------------------------------------
        //
        static CatalogData() {
            DayOfWeekToChrDic = new Dictionary<DayOfWeek, char>() {
                { DayOfWeek.Sunday, '日' },
                { DayOfWeek.Monday, '月' },
                { DayOfWeek.Tuesday, '火' },
                { DayOfWeek.Wednesday, '水' },
                { DayOfWeek.Thursday, '木' },
                { DayOfWeek.Friday, '金' },
                { DayOfWeek.Saturday, '土' }
            };

            ChrToDayOfWeekDic = new Dictionary<char, DayOfWeek>() {
                { '日', DayOfWeek.Sunday },
                { '月', DayOfWeek.Monday },
                { '火', DayOfWeek.Tuesday },
                { '水', DayOfWeek.Wednesday },
                { '木', DayOfWeek.Thursday },
                { '金', DayOfWeek.Friday },
                { '土', DayOfWeek.Saturday }
            };
        }
        #endregion ((static)Constructor)

        private const string REG_DB_FILE_NAME = @"^ccatalog\d+\.db$";
        private const string REG_CIRCLECUT_IMG_FILE = @"^C\d+CUTL\.CCZ$";
        private const string CIRCLECUTS_FOLDER_NAME = "circlecuts";
        private const string MAPS_LOCAL_FOLDER_NAME = "maps";
        private const string MAP_CATALOGDATA_DIR = "MDATA";
        /// <summary>{0}:dayIndex(1,2,3), {1}:filename(from db)</summary>
        private const string MAP_IMAGES_NAME_FORMAT = "MAP{0}{1}.PNG";
        private const string REG_MAP_IMAGES_NAME = @"^MAP\d.+\.PNG$";

        private const string GENREMAP1_IMAGES_NAME_FORMAT = "GBG{0}{1}.PNG";
        private const string REG_GENREMAP1_IMAGES_NAME = @"^GBG\d.+\.PNG$";
        private const string GENREMAP2_IMAGES_NAME_FORMAT = "GFG{0}{1}.PNG";
        private const string REG_GENREMAP2_IMAGES_NAME = @"^GFG\d.+\.PNG$";

        private SQLiteConnection _connSync = null;
        private SQLiteAsyncConnection _conn = null;
        private string _dbFilePath = null; // initialize in constructor

        //-------------------------------------------------------------------------------
        #region 早引き用データ
        //-------------------------------------------------------------------------------
        private ComiketInfo _comiketInfo = null;
        private Dictionary<char, int> _blockToId = null; // 文字でのSQL指定がうまくいかないのでその変わりに使うBlock文字からidへの辞書
        private Dictionary<int, char> _blockIdToChr = null; // BlockId -> Block文字

        private Dictionary<int, string> _genreIdToStr = null; // ジャンルID -> ジャンル文字列
        private Dictionary<string, int> _genreStrToId = null; // ジャンル文字列 -> ジャンルID 初期化は_genreIdToStrに伴う
        private Dictionary<int, string> _genreCodeToStr = null; // ジャンルコード -> ジャンル文字列
        private Dictionary<string, int> _genreStrToCode = null; // ジャンル文字列 -> ジャンルコード

        private Dictionary<int, Dictionary<string, char>> _day_areaToFirstBlock = new Dictionary<int, Dictionary<string, char>>(); // 日付 -> (エリア -> 最初のブロック文字)
        private Dictionary<char, string> _blockToArea = null; // ブロック -> エリア
        private Dictionary<char, char> _blockToAreaChar = null; // ブロック -> 東or西
        private Dictionary<char, string> _blockToBigArea = null; // ブロック -> 東123or東456or西12

        private Dictionary<DayOfWeek, int> _dayOfWeekToDayIndex = null;
        //-------------------------------------------------------------------------------
        #endregion (データ)

        //-------------------------------------------------------------------------------
        #region +[static]checkExistFiles ローカルにファイルが存在しているか確認
        //-------------------------------------------------------------------------------
        //
        public static async Task<bool> checkExistFiles() {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            var folders = await localDir.GetFoldersAsync();

            // db file
            if (!files.Any(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME))) { return false; }

            // map images
            if (!folders.Any(sf => sf.Name == MAPS_LOCAL_FOLDER_NAME)) { return false; }

            // circle cut images
            if (!folders.Any(sf => sf.Name == CIRCLECUTS_FOLDER_NAME)) { return false; }

            return true;
        }
        #endregion (+[static]checkExistFiles)
        //-------------------------------------------------------------------------------
        #region +[static]OpenCatalogData カタログデータ
        //-------------------------------------------------------------------------------
        /// <summary>
        /// カタログデータオープン．できない場合はnullが返る
        /// </summary>
        /// <returns></returns>
        public static async Task<CatalogData> OpenCatalogData() {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            var folders = await localDir.GetFoldersAsync();

            // map images
            if (!folders.Any(sf => sf.Name == MAPS_LOCAL_FOLDER_NAME)) { return null; }
            // circle cut images
            if (!folders.Any(sf => sf.Name == CIRCLECUTS_FOLDER_NAME)) { return null; }

            // db file
            var dbfile = files.LastOrDefault(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME));

            CatalogData data = new CatalogData(dbfile.Path);
            return data;
        }
        #endregion (OpenCatalogData)

        //-------------------------------------------------------------------------------
        #region +[static]copyToLocal 選択フォルダからローカルへコピー
        //-------------------------------------------------------------------------------
        //
        public async static Task copyToLocal(StorageFolder rootDir, Func<int, int, int, int, bool> progressReportFunc = null, Action<string> errorReportFunc = null) {
            const int ALL_TASKS = 3;
            int task_done = 0;

            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            Func<int, int, int, int, bool> report = (curr1, all1, curr2, all2) => {
                if (progressReportFunc != null) {
                    bool res = progressReportFunc(curr1, all1, curr2, all2);
                    return res;
                }
                return true;
            };

            // db file
            var files = await rootDir.GetFilesAsync();
            var dbfile = files.Where(sf => Regex.IsMatch(sf.Name, REG_DB_FILE_NAME))
                               .FirstOrDefault();

            if (dbfile == null && errorReportFunc != null) {
                errorReportFunc("No database file (ccatalog**.db)");
                return;
            }

            if (!report(task_done, ALL_TASKS, 0, 1)) { return; }
            await dbfile.CopyAsync(localDir, dbfile.Name, NameCollisionOption.ReplaceExisting);
            if (!report(task_done, ALL_TASKS, 1, 1)) { return; }

            ++task_done;
            // map image files
            var mapDir = await rootDir.GetFolderAsync(MAP_CATALOGDATA_DIR);
            if (mapDir == null && errorReportFunc != null) {
                errorReportFunc("No map folder (MDATA/)");
                return;
            }
            var mapDirFiles = await mapDir.GetFilesAsync();
            var mapFiles = mapDirFiles.Where(sf => Regex.IsMatch(sf.Name, REG_MAP_IMAGES_NAME)
                                                || Regex.IsMatch(sf.Name, REG_GENREMAP1_IMAGES_NAME)
                                                || Regex.IsMatch(sf.Name, REG_GENREMAP2_IMAGES_NAME)).ToArray();
            if (mapFiles.Length < 27 && errorReportFunc != null) {
                errorReportFunc("Shortage : Map image files (MAP****.PNG)");
                return;
            }

            var mapsDir = await localDir.CreateFolderAsync(MAPS_LOCAL_FOLDER_NAME, CreationCollisionOption.OpenIfExists);

            if (!report(task_done, ALL_TASKS, 0, mapFiles.Length)) { return; }
            for (int i = 0; i < mapFiles.Length; ++i) {
                await mapFiles[i].CopyAsync(mapsDir, mapFiles[i].Name, NameCollisionOption.ReplaceExisting);
                if (!report(task_done, ALL_TASKS, i + 1, mapFiles.Length)) { return; }
            }

            ++task_done;
            // circle cut files
            var circleCutsDir = await localDir.CreateFolderAsync(CIRCLECUTS_FOLDER_NAME, CreationCollisionOption.OpenIfExists);
            var circleCutFile = files.Where(sf => Regex.IsMatch(sf.Name, REG_CIRCLECUT_IMG_FILE))
                               .FirstOrDefault();
            if (circleCutFile == null && errorReportFunc != null) {
                errorReportFunc("No circle cut image file (C***CUTL.CCZ)");
                return;
            }

            using (MemoryStream ms = new MemoryStream()) {
                using (Stream stream = (await circleCutFile.OpenReadAsync()).AsStream()) {
                    byte[] buf = new byte[0x10000000];
                    while (true) {
                        int r = stream.Read(buf, 0, buf.Length);
                        if (r <= 0) { break; }
                        ms.Write(buf, 0, r);
                    }
                }

                using (var zipArchive = new ZipArchive(ms)) {

                    if (!report(task_done, ALL_TASKS, 0, zipArchive.Entries.Count)) { return; }
                    int count = 0;

                    foreach (var entry in zipArchive.Entries) {
                        string name = entry.Name;
                        using (Stream dstStr = await circleCutsDir.OpenStreamForWriteAsync(name, CreationCollisionOption.ReplaceExisting))
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
                            if (!report(task_done, ALL_TASKS, count, zipArchive.Entries.Count)) { return; }
                        }
                    }

                    // finish tasks
                    ++task_done;
                    if (!report(task_done, ALL_TASKS, count, zipArchive.Entries.Count)) { return; }
                }
            }
        }
        #endregion (+[static]copyToLocal)

        //-------------------------------------------------------------------------------
        #region +[static]GetCircleCutImageAsync サークルカット画像取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<BitmapImage> GetCircleCutImageAsync(int updateId) {
            return await CatalogData.GetImageAsync(CIRCLECUTS_FOLDER_NAME, string.Format("{0}.PNG", updateId));
        }
        #endregion (GetCircleCutImageAsync
        //-------------------------------------------------------------------------------
        #region +[static]GetMapImageAsync 地図画像取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<BitmapImage> GetMapImageAsync(string filename) {
            return await CatalogData.GetImageAsync(MAPS_LOCAL_FOLDER_NAME, filename);
        }
        #endregion (GetMapImageAsync))
        //-------------------------------------------------------------------------------
        #region -[static]GetImageAsync 画像取得
        //-------------------------------------------------------------------------------
        //
        private static async Task<BitmapImage> GetImageAsync(string dirName, string fileName) {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var imgDir = await localDir.GetFolderAsync(dirName);
            var file = await imgDir.GetFileAsync(fileName);
            using (var stream = await file.OpenReadAsync()) {
                BitmapImage bmp = new BitmapImage();
                bmp.SetSource(stream);
                return bmp;
            }
        }
        #endregion (-[static]GetImageAsync)

        //-------------------------------------------------------------------------------
        #region +[static]GetMapFileName 画像ファイル名
        //-------------------------------------------------------------------------------
        //
        public static string GetMapFileName(int dayIndex, string nameId) {
            return string.Format(MAP_IMAGES_NAME_FORMAT, dayIndex, nameId);
        }
        #endregion (GetMapFileName)
        //-------------------------------------------------------------------------------
        #region +[static]GetGenreMapFileNames 画像ファイル名
        //-------------------------------------------------------------------------------
        //
        public static Tuple<string, string> GetGenreMapFileNames(int dayIndex, string nameId) {
            return Tuple.Create(string.Format(GENREMAP1_IMAGES_NAME_FORMAT, dayIndex, nameId), string.Format(GENREMAP2_IMAGES_NAME_FORMAT, dayIndex, nameId));
        }
        #endregion (GetGenreMapFileNames)

        //-------------------------------------------------------------------------------
        #region -[static]ConvertToWideChar 半角アルファベットを全角に変換
        //-------------------------------------------------------------------------------
        //
        private static char ConvertToWideChar(char c)
        {
            const string HALF = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string WIDE = "ＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ";

            if (HALF.Contains(c)) {
                return WIDE[HALF.IndexOf(c)];
            }
            return c;
        }
        #endregion (ConvertToWideChar)

        public bool Connected { get; private set; }

        private CatalogData(string dbFilePath) {
            Connected = false;
            _dbFilePath = dbFilePath;
        }

        //-------------------------------------------------------------------------------
        #region +CloseConnection
        //-------------------------------------------------------------------------------
        //
        public void CloseConnection() {
            if (Connected) {
                //_conn.Close();
                _conn = null;
                Connected = false;
            }
        }
        #endregion (CloseConnection)

        //-------------------------------------------------------------------------------
        #region -connect 接続
        //-------------------------------------------------------------------------------
        //
        private void connect() {
            _connSync = new SQLiteConnection(_dbFilePath);
            _conn = new SQLiteAsyncConnection(_dbFilePath);
            Connected = true;
        }
        #endregion (connect)
        //-------------------------------------------------------------------------------
        #region -InitDicsOfBlockAndId ブロック<->idの辞書
        //-------------------------------------------------------------------------------
        //
        private void InitDicsOfBlockAndId(IEnumerable<ComiketBlock> cbs) {
            _blockToId = new Dictionary<char, int>();
            _blockIdToChr = new Dictionary<int, char>();
            foreach (var cb in cbs) {
                _blockToId.Add(cb.name[0], cb.id);
                _blockIdToChr.Add(cb.id, cb.name[0]);
            }
        }
        #endregion (InitDicsOfBlockAndId)
        //-------------------------------------------------------------------------------
        #region -InitDicsOfGenreIdAndStr
        //-------------------------------------------------------------------------------
        //
        private void InitDicsOfGenreIdAndStr(IEnumerable<ComiketGenre> cgs) {
            _genreIdToStr = new Dictionary<int, string>();
            _genreStrToId = new Dictionary<string, int>();
            _genreCodeToStr = new Dictionary<int, string>();
            _genreStrToCode = new Dictionary<string, int>();
            foreach (var cg in cgs) {
                _genreIdToStr.Add(cg.id, cg.name);
                _genreStrToId.Add(cg.name, cg.id);

                _genreCodeToStr.Add(cg.code, cg.name);
                _genreStrToCode.Add(cg.name, cg.code);
            }
        }
        #endregion (InitGenreIdToStrDic)
        //-------------------------------------------------------------------------------
        #region -InitBlockToAreaDic
        //-------------------------------------------------------------------------------
        //
        private void InitBlockToAreaDic(IEnumerable<ComiketArea> cas, IEnumerable<ComiketBlock> cbs) {
            _blockToArea = new Dictionary<char, string>();
            _blockToAreaChar = new Dictionary<char, char>();

            foreach (var cb in cbs) {
                var first = cas.Where(ca => ca.id == cb.areaId)
                               .First();
                _blockToArea.Add(cb.name[0], first.name);
                _blockToAreaChar.Add(cb.name[0], first.simpleName[0]);
            }
        }
        #endregion (InitDicOfAreaAndBlock)
        //-------------------------------------------------------------------------------
        #region -InitBlockToBigAreaDic
        //-------------------------------------------------------------------------------
        //
        private void InitBlockToBigAreaDic(IEnumerable<ComiketArea> cas, IEnumerable<ComiketBlock> cbs, IEnumerable<ComiketMap> cms) {
            _blockToBigArea = new Dictionary<char, string>();
            foreach (var cb in cbs) {
                string bigarea = cas.Where(ca => ca.id == cb.areaId)
                                     .SelectMany(ca => cms.Where(cm => cm.id == ca.mapId))
                                     .Select(cm => cm.name)
                                     .FirstOrDefault();
                Debug.Assert(bigarea != null);
                _blockToBigArea.Add(cb.name[0], bigarea);
            }
        }
        #endregion (InitBlockToBigAreaDic)
        //-------------------------------------------------------------------------------
        #region -InitAreaToFirstBlockDic
        //-------------------------------------------------------------------------------
        //
        private void InitAreaToFirstBlockDic(int day, IEnumerable<ComiketArea> cas, IEnumerable<ComiketBlock> cbs, char[] exist_blocks) {
            var dic = new Dictionary<string, char>();
            foreach (var ca in cas) {
                var first = cbs.Where(cb => cb.areaId == ca.id)
                               .Where(cb => exist_blocks.Contains(cb.name[0]))
                               .OrderBy(cb => cb.name[0], ComiketUtil.BlockComparer.GetInstance())
                               .First();
                dic.Add(ca.name, first.name[0]);
            }

            _day_areaToFirstBlock.Add(day, dic);
        }
        #endregion (InitAreaToFirstBlockDic)
        //-------------------------------------------------------------------------------
        #region -InitDayOfWeekToDayIndexDic
        //-------------------------------------------------------------------------------
        //
        private void InitDayOfWeekToDayIndexDic(IEnumerable<ComiketDate> days) {
            _dayOfWeekToDayIndex = new Dictionary<DayOfWeek, int>();
            foreach (var item in days) {
                _dayOfWeekToDayIndex.Add((new DateTime(item.year, item.month, item.day)).DayOfWeek, item.id);
            }
        }
        #endregion (InitDayOfWeekToDayIndexDic)

        //-------------------------------------------------------------------------------
        #region +GetDayIndex 曜日->日付インデックス
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetDayIndex(DayOfWeek day) {
            if (!Connected) { connect(); }
            if (_dayOfWeekToDayIndex == null) {
                var res = await DBQueriesAsync.GetDays(_conn);
                InitDayOfWeekToDayIndexDic(res);
            }
            return _dayOfWeekToDayIndex[day];
        }
        //
        public async Task<int> GetDayIndex(char day) {
            return await GetDayIndex(CatalogData.ChrToDayOfWeekDic[day]);
        }
        #endregion (GetDayIndex)
        //-------------------------------------------------------------------------------
        #region +GetComiketInfo コミケ情報
        //-------------------------------------------------------------------------------
        /// <summary>
        /// コミケ情報取得
        /// </summary>
        public async Task<ComiketInfo> GetComiketInfo() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetComiketInfo(_conn);
            if (_comiketInfo == null) { _comiketInfo = res; }
            return res;
        }
        #endregion (GetComiketInfo)
        //-------------------------------------------------------------------------------
        #region +GetDays 曜日一覧
        //-------------------------------------------------------------------------------
        //
        public async Task<DayOfWeek[]> GetDays() {
            if (!Connected) { connect(); }
            var dates = await DBQueriesAsync.GetDays(_conn);
            if (_dayOfWeekToDayIndex == null) { InitDayOfWeekToDayIndexDic(dates); }
            return dates.Select(cd => (new DateTime(cd.year, cd.month, cd.day)).DayOfWeek).ToArray();
        }
        #endregion (GetDays)
        //-------------------------------------------------------------------------------
        #region +GetAreas エリア一覧(東123,東1,...等)
        //-------------------------------------------------------------------------------
        //
        public async Task<string[]> GetAreas() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetAreas(_conn);
            return res.Select(ca => ca.name).ToArray();
        }
        #endregion (GetAreas)
        //-------------------------------------------------------------------------------
        #region +GetBlocks ブロック一覧(あいうABC...等)
        //-------------------------------------------------------------------------------
        //
        public async Task<char[]> GetBlocks() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetBlocks(_conn);

            if (_blockToId == null) { InitDicsOfBlockAndId(res); }
            return res.Select(cb => cb.name[0]).ToArray();
        }
        #endregion (+GetBlocks)
        //-------------------------------------------------------------------------------
        #region +GetGenres ジャンル一覧(文字列)
        //-------------------------------------------------------------------------------
        //
        public async Task<string[]> GetGenres() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetGenres(_conn);
            if (_genreIdToStr == null) { InitDicsOfGenreIdAndStr(res); }
            return res.Select(cg => cg.name).ToArray();
        }
        #endregion (GetGenres)
        //-------------------------------------------------------------------------------
        #region +GetMapNames マップ名前
        //-------------------------------------------------------------------------------
        //
        public async Task<string[]> GetMapNames() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetMaps(_conn);
            return res.Select(cm => cm.name).ToArray();
        }
        #endregion (GetMapNames)

        //-------------------------------------------------------------------------------
        #region +GetBlocksOfDay 特定の日付に存在するブロックを返す
        //-------------------------------------------------------------------------------
        //
        public async Task<char[]> GetBlocksOfDay(int day) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) {
                var resb = await DBQueriesAsync.GetBlocks(_conn);
                InitDicsOfBlockAndId(resb);
            }

            var res = await DBQueriesAsync.GetBlocksOfDay(_conn, day);
            return res.Select(id => _blockIdToChr[id]).ToArray();
        }
        #endregion (GetBlocksOfDay)

        //-------------------------------------------------------------------------------
        #region +GetGenreStringById ジャンルId->ジャンル文字列
        //-------------------------------------------------------------------------------
        //
        public async Task<string> GetGenreStringById(int id) {
            if (!Connected) { connect(); }
            if (_genreIdToStr == null) { await GetGenres(); }
            return _genreIdToStr[id];
        }
        #endregion (GetGenreStringById)
        //-------------------------------------------------------------------------------
        #region +GetGenreStringByCode ジャンルコード->ジャンル文字列
        //-------------------------------------------------------------------------------
        //
        public async Task<string> GetGenreStringByCode(int code) {
            if (!Connected) { connect(); }
            if (_genreCodeToStr == null) { await GetGenres(); }
            return _genreCodeToStr[code];
        }
        #endregion (GetGenreStringByCode)
        //-------------------------------------------------------------------------------
        #region +GetGenreCode ジャンル文字列->ジャンルコード
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetGenreCode(string genreStr) {
            if (!Connected) { connect(); }
            if (_genreStrToCode == null) { await GetGenres(); }
            return _genreStrToCode[genreStr];
        }
        #endregion (GetGenreCode)

        //-------------------------------------------------------------------------------
        #region +GetBlockChr ブロック文字
        //-------------------------------------------------------------------------------
        //
        public async Task<char> GetBlockChr(int id) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) { await GetBlocks(); }
            return _blockIdToChr[id];
        }
        #endregion (GetBlockChr)
        //-------------------------------------------------------------------------------
        #region +GetFirstBlock エリア文字列->最初のブロック文字
        //-------------------------------------------------------------------------------
        //
        public async Task<char> GetFirstBlock(int day, string area) {
            if (!Connected) { connect(); }
            if (!_day_areaToFirstBlock.ContainsKey(day)) {
                var areas = await DBQueriesAsync.GetAreas(_conn);
                var blocks = await DBQueriesAsync.GetBlocks(_conn);
                var existBlocks = await GetBlocksOfDay(day);
                InitAreaToFirstBlockDic(day, areas, blocks, existBlocks);
            }
            return _day_areaToFirstBlock[day][area];
        }
        #endregion (GetFirstBlock)
        //-------------------------------------------------------------------------------
        #region +GetArea ブロック文字->エリア文字列
        //-------------------------------------------------------------------------------
        //
        public async Task<string> GetArea(char block) {
            if (!Connected) { connect(); }
            if (_blockToArea == null) {
                var areas = await DBQueriesAsync.GetAreas(_conn);
                var blocks = await DBQueriesAsync.GetBlocks(_conn);
                InitBlockToAreaDic(areas, blocks);
            }
            return _blockToArea[CatalogData.ConvertToWideChar(block)];
        }
        public async Task<string> GetArea(int blockId) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) { await GetBlocks(); }
            return await GetArea(_blockIdToChr[blockId]);
        }
        #endregion (GetArea)

        //-------------------------------------------------------------------------------
        #region +GetFirstCirclePositionOfGenre ジャンルの最初のサークルの位置
        //-------------------------------------------------------------------------------
        /// <summary>
        /// return (day, block, offset)
        /// </summary>
        public async Task<Tuple<int, char, int>> GetFirstCirclePositionOfGenre(string genreStr) {
            if (!Connected) { connect(); }
            if (_genreStrToId == null) { await GetGenres(); }
            if (_blockIdToChr == null) { await GetBlocks(); }

            var res = await DBQueriesAsync.GetCirclesInGenre(_conn, _genreStrToId[genreStr]);
            var circle = res.FirstOrDefault();
            return (circle == null) ? null : Tuple.Create(circle.day, _blockIdToChr[circle.blockId], (circle.spaceNo - 1) * 2 + circle.spaceNoSub);
        }
        #endregion (GetFirstCircleOfGenre)

        //-------------------------------------------------------------------------------
        #region +GetCircles サークル
        //-------------------------------------------------------------------------------
        //
        public async Task<ComiketCircle[]> GetCircles(int day, char block) {
            if (!Connected) { connect(); }
            if (_blockToId == null) { await GetBlocks(); }
            var res = await DBQueriesAsync.GetCircles(_conn, day, _blockToId[block]);
            return res.ToArray();
        }
        #endregion (GetCircles)

        //-------------------------------------------------------------------------------
        #region +[async]GetMap マップ画像取得
        //-------------------------------------------------------------------------------
        //
        public async Task<BitmapImage> GetMap(int day, char block) {
            if (!Connected) { connect(); }
            if (_blockToArea == null) {
                var areas = await DBQueriesAsync.GetAreas(_conn);
                var blocks = await DBQueriesAsync.GetBlocks(_conn);
                InitBlockToAreaDic(areas, blocks);
            }
            return await GetMap(day, _blockToArea[block]);
        }
        public async Task<BitmapImage> GetMap(int day, int blockId) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) { await GetBlocks(); }
            return await GetMap(day, _blockIdToChr[blockId]);
        }
        public async Task<BitmapImage> GetMap(int day, string area) {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetMapInfoFromArea(_conn, area);
            var elem = res.FirstOrDefault();
            var img = await CatalogData.GetMapImageAsync(CatalogData.GetMapFileName(day, elem.filename));
            return img;
        }
        #endregion (GetMap)
        //-------------------------------------------------------------------------------
        #region +[async]GetAllMaps マップ画像取得
        //-------------------------------------------------------------------------------
        //
        public async Task<BitmapImage[]> GetAllMaps() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetMaps(_conn);
            var tasks = Enumerable.Range(1, 3).SelectMany(day => res.Select(async cm => await CatalogData.GetMapImageAsync(CatalogData.GetMapFileName(day, cm.filename)))).ToArray();
            return await convertAsyncArray(tasks);
        }
        #endregion (GetMaps)

        //-------------------------------------------------------------------------------
        #region +GetAllGenreInfoMaps 重畳表示用ジャンル地図
        //-------------------------------------------------------------------------------
        //
        public async Task<Tuple<BitmapImage, BitmapImage>[]> GetAllGenreInfoMaps() {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetMaps(_conn);
            var tasks = Enumerable.Range(1, 3)
                                  .SelectMany(day => res.Select(cm => CatalogData.GetGenreMapFileNames(day, cm.filename))
                                                        .Select(tuple => Tuple.Create(CatalogData.GetMapImageAsync(tuple.Item1),
                                                                                      CatalogData.GetMapImageAsync(tuple.Item2))));
            var c1 = tasks.Select(convertAsyncTuple).ToArray();
            return await convertAsyncArray(c1);
        }
        #endregion (GetMapGenreInfo)

        //-------------------------------------------------------------------------------
        #region +GetMapPlace マップ上の位置
        //-------------------------------------------------------------------------------
        //
        public async Task<Tuple<int, int, int>> GetMapPlace(int blockId, int spaceNo) {
            if (!Connected) { connect(); }
            if (_blockToId == null) { await GetBlocks(); }

            var res = await DBQueriesAsync.GetLayouts(_conn, blockId, spaceNo);
            var elem = res.First();

            return Tuple.Create(elem.xpos, elem.ypos, elem.layout);
        }
        #endregion (GetMapPlace)
        //-------------------------------------------------------------------------------
        #region +GetAreaChar 東か西か
        //-------------------------------------------------------------------------------
        //
        public async Task<char> GetAreaChar(int blockId) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) { await GetBlocks(); }
            return await GetAreaChar(_blockIdToChr[blockId]);
        }
        //
        public async Task<char> GetAreaChar(char block) {
            if (!Connected) { connect(); }
            if (_blockToAreaChar == null) {
                var areas = await DBQueriesAsync.GetAreas(_conn);
                var blocks = await DBQueriesAsync.GetBlocks(_conn);
                InitBlockToAreaDic(areas, blocks);
            }

            return _blockToAreaChar[CatalogData.ConvertToWideChar(block)];
        }
        #endregion (GetBigArea)
        //-------------------------------------------------------------------------------
        #region +GetBigArea 東123or東456or西12
        //-------------------------------------------------------------------------------
        //
        public async Task<string> GetBigArea(char block) {
            if (!Connected) { connect(); }
            if (_blockToBigArea == null) {
                var areas = await DBQueriesAsync.GetAreas(_conn);
                var blocks = await DBQueriesAsync.GetBlocks(_conn);
                var maps = await DBQueriesAsync.GetMaps(_conn);
                InitBlockToBigAreaDic(areas, blocks, maps);
            }
            return _blockToBigArea[CatalogData.ConvertToWideChar(block)];
        }
        public async Task<string> GetBigArea(int blockId) {
            if (!Connected) { connect(); }
            if (_blockIdToChr == null) { await GetBlocks(); }
            return await GetBigArea(_blockIdToChr[blockId]);
        }
        #endregion (GetBigArea)



        //-------------------------------------------------------------------------------
        #region -convertAsync<T>
        //-------------------------------------------------------------------------------
        /// <summary>
        /// Task<T>[] -> Task<T[]>
        /// </summary>
        private static async Task<T[]> convertAsyncArray<T>(Task<T>[] from) {
            List<T> list = new List<T>();
            foreach (var item in from) {
                list.Add(await item);
            }
            return list.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        private static async Task<Tuple<T, U>> convertAsyncTuple<T, U>(Tuple<Task<T>, Task<U>> from) {
            return Tuple.Create(await from.Item1, await from.Item2);
        }
        #endregion (-convertAsync<T>)

        //-------------------------------------------------------------------------------
        #region +SearchCirclesByStr 文字列でサークル検索
        //-------------------------------------------------------------------------------
        //
        public async Task<List<ComiketCircleAndLayout>> SearchCirclesByStr(string queryStr) {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetCirclesByString(_conn, queryStr);
            return res.ToList();
        }
        #endregion (SearchCirclesByStr)
        //-------------------------------------------------------------------------------
        #region +SearchCirclesByGenre ジャンルでサークル検索
        //-------------------------------------------------------------------------------
        //
        public async Task<List<ComiketCircleAndLayout>> SearchCirclesByGenre(string genreStr) {
            if (!Connected) { connect(); }
            if (_genreStrToId == null) { await GetGenres(); }
            var res = await DBQueriesAsync.GetCirclesInGenre(_conn, _genreStrToId[genreStr]);
            return res.ToList();
        }
        #endregion (SearchCirclesByGenre)

        //-------------------------------------------------------------------------------
        #region +GetCirclesCount カウント
        //-------------------------------------------------------------------------------
        //
        public int GetCirclesCount() {
            if (!Connected) { connect(); }
            var res = DBQueriesSync.GetCirclesCount(_connSync);
            return res;
        }
        #endregion (GetCirclesCount)

        //-------------------------------------------------------------------------------
        #region +GetCircleFromId updateIdでサークル取得
        //-------------------------------------------------------------------------------
        //
        public async Task<ComiketCircleAndLayout> GetCircleFromId(int updateId)
        {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetCircleFromUpdateId(_conn, updateId);
            return res.FirstOrDefault();
        }
        #endregion (GetCircleFromId)

        //-------------------------------------------------------------------------------
        #region +GetCirclesFromIndex インデックスでサークル取得
        //-------------------------------------------------------------------------------
        //
        public ComiketCircle[] GetCirclesFromIndex(int startIndex, int num) {
            if (!Connected) { connect(); }
            var circles = DBQueriesSync.GetCirclesFromIndex(_connSync, startIndex, startIndex + num - 1);
            return circles.ToArray();
        }
        #endregion (GetCirclesFromIndex)

        //-------------------------------------------------------------------------------
        #region +GetCirclesFromIndexAsync インデックスでサークル取得
        //-------------------------------------------------------------------------------
        //
        public async Task<ComiketCircle[]> GetCirclesFromIndexAsync(int startIndex, int num) {
            if (!Connected) { connect(); }
            var circles = await DBQueriesAsync.GetCirclesFromIndex(_conn, startIndex, startIndex + num - 1);
            return circles.ToArray();
        }
        #endregion (GetCirclesFromIndex)

        //-------------------------------------------------------------------------------
        #region GetFirstCircleIndexOfDay
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetFirstCircleIndexOfDay(int dayIndex) {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetMinimumIndexOfCirclesOfDay(_conn, dayIndex);
            return res;
        }
        #endregion (GetFirstCircleIndexOfDay)
        //-------------------------------------------------------------------------------
        #region GetFirstCircleIndexOfDayAndBlock
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetFirstCircleIndexOfDayAndBlock(int dayIndex, char block) {
            if (!Connected) { connect(); }
            if (_blockToId == null) { await GetBlocks(); }
            var res = await DBQueriesAsync.GetMinimumIndexOfCirclesOfDayAndBlock(_conn, dayIndex, _blockToId[block]);
            return res;
        }
        #endregion (GetFirstCircleIndexOfDayAndBlock)
        //-------------------------------------------------------------------------------
        #region GetFirstCircleIndexOfGenre
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetFirstCircleIndexOfGenre(string genre) {
            if (!Connected) { connect(); }
            if (_genreStrToId == null) { await GetGenres(); }
            var res = await DBQueriesAsync.GetMinimumIndexOfCirclesOfGenre(_conn, _genreStrToId[genre]);
            return res;
        }
        #endregion (GetFirstCircleIndexOfGenre)
        //-------------------------------------------------------------------------------
        #region GetIdOfCircle
        //-------------------------------------------------------------------------------
        //
        public async Task<int> GetIdOfCircle(int updateId) {
            if (!Connected) { connect(); }
            var res = await DBQueriesAsync.GetIndexOfCircle(_conn, updateId);
            return res;
        }
        #endregion (GetIdOfCircle)
        
    }

    static class DBQueriesAsync {
        //-------------------------------------------------------------------------------
        #region +[static]GetComiketInfo カタログデータ取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<ComiketInfo> GetComiketInfo(SQLiteAsyncConnection conn) {
            var res = await conn.QueryAsync<ComiketInfo>("select * from ComiketInfo");
            Debug.Assert(res.Count == 1);
            return res[0];
        }
        #endregion (GetComiketInfo)

        //-------------------------------------------------------------------------------
        #region +[static]GetDays 日一覧取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketDate>> GetDays(SQLiteAsyncConnection conn) {
            return await conn.QueryAsync<ComiketDate>("select * from ComiketDate");
        }
        #endregion (+[static]GetDays)
        //-------------------------------------------------------------------------------
        #region +[static]GetMaps マップ情報一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketMap>> GetMaps(SQLiteAsyncConnection conn) {
            return await conn.QueryAsync<ComiketMap>("select * from ComiketMap");
        }
        #endregion (+[static]GetMaps)
        //-------------------------------------------------------------------------------
        #region +[static]GetMapInfoFromArea マップ情報取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketMap>> GetMapInfoFromArea(SQLiteAsyncConnection conn, string area) {
            return await conn.QueryAsync<ComiketMap>("select cm.* from ComiketMap cm, ComiketArea ca where cm.id = ca.mapId and ca.name = ?", area);
        }
        #endregion (GetMapInfoFromArea)
        //-------------------------------------------------------------------------------
        #region +[static]GetAreas エリア一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketArea>> GetAreas(SQLiteAsyncConnection conn) {
            return await conn.QueryAsync<ComiketArea>("select * from ComiketArea");
        }
        #endregion (GetAreaNames)
        //-------------------------------------------------------------------------------
        #region +[static]GetBlocks ブロック一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketBlock>> GetBlocks(SQLiteAsyncConnection conn) {
            return await conn.QueryAsync<ComiketBlock>("select * from ComiketBlock");
        }
        #endregion (GetBlockNames)
        //-------------------------------------------------------------------------------
        #region +[static]GetGenres ジャンル名前一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketGenre>> GetGenres(SQLiteAsyncConnection conn) {
            return await conn.QueryAsync<ComiketGenre>("select * from ComiketGenre");
        }
        #endregion (GetGenreNames)
        //-------------------------------------------------------------------------------
        #region +[static]GetLayouts レイアウト一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketLayout>> GetLayouts(SQLiteAsyncConnection conn, int blockId, int spaceNo) {
            return await conn.QueryAsync<ComiketLayout>("select * from ComiketLayout where blockId = ? and spaceNo = ?", blockId, spaceNo);
        }
        #endregion (GetLayouts)

        //-------------------------------------------------------------------------------
        #region +[static]GetCircles サークル一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketCircle>> GetCircles(SQLiteAsyncConnection conn, int day, int blockId) {
            return await conn.QueryAsync<ComiketCircle>(@"select C.* from ComiketCircle C, ComiketBlock B where C.blockId = B.id and C.day = ? and B.id = ?", day, blockId);
        }
        #endregion (GetCircles)
        //-------------------------------------------------------------------------------
        #region +[static]GetCirclesInGenre サークル一覧
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketCircleAndLayout>> GetCirclesInGenre(SQLiteAsyncConnection conn, int genreId) {
            return await conn.QueryAsync<ComiketCircleAndLayout>(@"select cc.*, cl.* from ComiketCircle cc, ComiketLayout cl where cc.genreId = ? and cl.blockId = cc.blockId and cl.spaceNo = cc.spaceNo and cc.day <> 0", genreId);
        }
        #endregion (GetCircles)
        //-------------------------------------------------------------------------------
        #region +[static]GetCirclesByString 文字列でサークル検索
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketCircleAndLayout>> GetCirclesByString(SQLiteAsyncConnection conn, string queryStr) {
            return await conn.QueryAsync<ComiketCircleAndLayout>(string.Format(@"select cc.*, cl.* from ComiketCircle cc, ComiketLayout cl where day <> 0 and cl.blockId = cc.blockId and cl.spaceNo = cc.spaceNo and (bookName like ""%{0}%"" or description like ""%{0}%"")", queryStr));
        }
        #endregion (GetCirclesByString)

        //-------------------------------------------------------------------------------
        #region +[static]GetAreaFromBlockId ブロックIDからエリアを取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketArea>> GetAreaFromBlockId(SQLiteAsyncConnection conn, int blockId) {
            return await conn.QueryAsync<ComiketArea>("select ca.* from ComiketArea ca, ComiketBlock cb where ca.id = cb.areaId and cb.id = ?", blockId);
        }
        #endregion (GetAreaFromBlockId)

        //-------------------------------------------------------------------------------
        #region +[static]GetBlocksOfDay 特定日付内の，サークルが存在するブロック一覧を返す
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<int>> GetBlocksOfDay(SQLiteAsyncConnection conn, int dayIndex) {

            return (await conn.QueryAsync<BlockID>("select distinct blockId from ComiketCircle where day = ?", dayIndex))
                       .Select(bid => bid.blockId);
        }
        #endregion (GetBlocksOfDay)

        //-------------------------------------------------------------------------------
        #region GetCircleFromUpdateId UpdateIDからサークル取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketCircleAndLayout>> GetCircleFromUpdateId(SQLiteAsyncConnection conn, int updateId)
        {
            return await conn.QueryAsync<ComiketCircleAndLayout>(@"select cc.*, cl.* from ComiketCircle cc, ComiketLayout cl where day <> 0 and cl.blockId = cc.blockId and cl.spaceNo = cc.spaceNo and cc.updateId = " + updateId.ToString());
        }
        #endregion (GetCircleFromUpdateId)
        //-------------------------------------------------------------------------------
        #region +[static]GetCircleFromIndex インデックスからサークル取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<IEnumerable<ComiketCircle>> GetCirclesFromIndex(SQLiteAsyncConnection conn, int startIndex, int endIndex) {
            var res = await conn.QueryAsync<ComiketCircle>(string.Format("select * from ComiketCircle where id >= {0} and id <= {1}", startIndex + 1, endIndex + 1));
            return res;
        }
        #endregion (GetCircleFromIndex)

        //-------------------------------------------------------------------------------
        #region +[static]GetMinimumIndexOfCirclesOfDay 日付の中の最小idを返す
        //-------------------------------------------------------------------------------
        //
        public static async Task<int> GetMinimumIndexOfCirclesOfDay(SQLiteAsyncConnection conn, int dayIndex) {
            var res = await conn.QueryAsync<IntVal>(string.Format("select MIN(id) as val from ComiketCircle where day = {0}", dayIndex));
            return res.First().val - 1;
        }
        #endregion (GetMinimumIndexOfCirclesOfDay)
        //-------------------------------------------------------------------------------
        #region +[static]GetMinimumIndexOfCirclesOfDayAndBlock 日付，ブロック指定の最小idを返す
        //-------------------------------------------------------------------------------
        //
        public static async Task<int> GetMinimumIndexOfCirclesOfDayAndBlock(SQLiteAsyncConnection conn, int dayIndex, int blockId) {
            var res = await conn.QueryAsync<IntVal>(string.Format("select MIN(id) as val from ComiketCircle where day = {0} and blockId = {1}", dayIndex, blockId));
            return res.First().val - 1;
        }
        #endregion (GetMinimumIndexOfCirclesOfDayAndBlock)
        //-------------------------------------------------------------------------------
        #region +[static]GetMinimumIndexOfCirclesOfGenre ジャンルでの最小id
        //-------------------------------------------------------------------------------
        //
        public static async Task<int> GetMinimumIndexOfCirclesOfGenre(SQLiteAsyncConnection conn, int genreId) {
            var res = await conn.QueryAsync<IntVal>(string.Format("select MIN(id) as val from ComiketCircle where genreId = {0}", genreId));
            return res.First().val - 1;
        }
        #endregion (GetMinimumIndexOfCirclesOfGenre)

        //-------------------------------------------------------------------------------
        #region +[static]GetIndexOfCircle サークルのSerialIDから連番ID取得
        //-------------------------------------------------------------------------------
        //
        public static async Task<int> GetIndexOfCircle(SQLiteAsyncConnection conn, int updateId) {
            var res = await conn.QueryAsync<IntVal>(string.Format("select id as val from ComiketCircle where updateId = {0}", updateId));
            return res.First().val - 1;
        }
        #endregion (+[static]GetIndexOfCircle)
    }
    public static class DBQueriesSync {
        //-------------------------------------------------------------------------------
        #region +[static]GetCirclesCount サークルカウント
        //-------------------------------------------------------------------------------
        //
        public static int GetCirclesCount(SQLiteConnection conn) {
            var res = conn.Query<IntVal>("select COUNT(*) as val from ComiketCircle");
            return res.First().val;
        }
        #endregion (GetCirclesCount)
        //-------------------------------------------------------------------------------
        #region +[static]GetCircleFromIndex インデックスからサークル取得
        //-------------------------------------------------------------------------------
        //
        public static IEnumerable<ComiketCircle> GetCirclesFromIndex(SQLiteConnection conn, int startIndex, int endIndex) {
            var res = conn.Query<ComiketCircle>(string.Format("select * from ComiketCircle where id >= {0} and id <= {1}", startIndex + 1, endIndex + 1));
            return res;
        }
        #endregion (GetCircleFromIndex)
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

    public class ComiketMap {
        [PrimaryKey]
        public int id { get; set; }
        public string name { get; set; }
        public string filename { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }
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
        [PrimaryKey]
        public int id { get; set; }
        public string name { get; set; }
        public string simpleName { get; set; }
        public int mapId { get; set; }
    }

    public class ComiketBlock {
        [PrimaryKey]
        public int id { get; set; }
        public string name { get; set; }
        public int areaId { get; set; }
    }

    public class ComiketGenre {
        [PrimaryKey]
        public int id { get; set; }
        public string name { get; set; }
        public int code { get; set; }
    }

    public class ComiketLayout {
        [PrimaryKey]
        public int blockId { get; set; }
        [PrimaryKey]
        public int spaceNo { get; set; }
        public int xpos { get; set; }
        public int ypos { get; set; }
        public int layout { get; set; }
    }

    public class ComiketCircle {
        public int day { get; set; }
        public int spaceNo { get; set; }
        public int spaceNoSub { get; set; }
        public int blockId { get; set; }
        public int genreId { get; set; }
        public int pageNo { get; set; }
        public int cutIndex { get; set; }
        public string circleName { get; set; }
        public string circleKana { get; set; }
        public string penName { get; set; }
        public string bookName { get; set; }
        public string url { get; set; }
        public string mailAddr { get; set; }
        public string description { get; set; }
        public string memo { get; set; }
        public int updateId { get; set; }
        public string updateData { get; set; }
        public string circlems { get; set; }
        public string rss { get; set; }

        public string SpaceStr { get { return string.Format("{0:D2}{1}", spaceNo, (spaceNoSub == 0) ? 'a' : 'b'); } }
    }

    public class BlockID {
        public int blockId { get; set; }
    }

    public class ComiketCircleAndLayout {
        public int day { get; set; }
        public int spaceNo { get; set; }
        public int spaceNoSub { get; set; }
        public int blockId { get; set; }
        public int genreId { get; set; }
        public int pageNo { get; set; }
        public int cutIndex { get; set; }
        public string circleName { get; set; }
        public string circleKana { get; set; }
        public string penName { get; set; }
        public string bookName { get; set; }
        public string url { get; set; }
        public string mailAddr { get; set; }
        public string description { get; set; }
        public string memo { get; set; }
        public int updateId { get; set; }
        public string updateData { get; set; }
        public string circlems { get; set; }
        public string rss { get; set; }

        public int xpos { get; set; }
        public int ypos { get; set; }
        public int layout { get; set; }

        public string SpaceStr { get { return string.Format("{0:D2}{1}", spaceNo, (spaceNoSub == 0) ? 'a' : 'b'); } }
    }

    public class IntVal {
        public int val { get; set; }
    }
    //-------------------------------------------------------------------------------
    #endregion (Classes for SQLite DB)
}