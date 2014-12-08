using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.Storage;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Windows.Storage.Streams;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace ComicStarViewer
{
    public class CheckList : INotifyPropertyChanged
    {
        private const string CHECKLIST_LOCAL_FILENAME = "checklist.csv";
        private const string CHECKLIST_TIME_LOCAL_FILENAME = "checklist_time.xml";

        private const string CHECKLIST_HEADER_COMICMARKET_STR = "ComicMarket";
        private const string CHECKLIST_HEADER_SIGNATURE = "ComicMarketCD-ROMCatalog";
        //-------------------------------------------------------------------------------
        #region +[static]CheckExistCheckList 保存済みチェックリストがあるか確認
        //-------------------------------------------------------------------------------
        //
        public static async Task<bool> CheckExistCheckList()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            return files.Any(sf => sf.Name == CHECKLIST_LOCAL_FILENAME);
        }
        #endregion (CheckExistCheckList)
        //-------------------------------------------------------------------------------
        #region +[static]OpenExistCheckList 保存済みチェックリストを開く
        //-------------------------------------------------------------------------------
        //
        public static async Task<StorageFile> OpenExistCheckList()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            var file = files.Where(sf => sf.Name == CHECKLIST_LOCAL_FILENAME)
                            .FirstOrDefault();
            if (file == null) { return null; }

            return file;
        }
        #endregion (OpenExistCheckList)
        //-------------------------------------------------------------------------------
        #region +[static]CopyToLocal ローカルにチェックリストを保存
        //-------------------------------------------------------------------------------
        //
        public static async Task CopyToLocal(StorageFile file)
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            await file.CopyAsync(localDir, CHECKLIST_LOCAL_FILENAME, NameCollisionOption.ReplaceExisting);
        }
        #endregion (CopyToLocal)

        //-------------------------------------------------------------------------------
        #region Constructor
        //-------------------------------------------------------------------------------
        //
        public CheckList(int comiketNo)
        {
            _comiketNo = comiketNo;
            SetDefaultColors();
        }
        #endregion (Constructor)

        //-------------------------------------------------------------------------------
        #region +OpenCheckList チェックリストを開く
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> OpenCheckList(StorageFile file, Func<char, Task<int>> dayToDayIndexFunc, Func<int, Task<ComiketCircleAndLayout>> updateIdToCircle)
        {
            string enc_str = null;
            using (var str = await file.OpenReadAsync())
            using (StreamReader sr = new StreamReader(str.AsStreamForRead())) {
                string line = sr.ReadLine();
                enc_str = CheckList.CheckEncode(line);
                if (enc_str == null) { return false; }
                int comiketNo = CheckList.CheckComiketNo(line);
                if (comiketNo != _comiketNo) { return false; }
            }

            Encoding enc = Encoding.GetEncoding(enc_str);

            bool res;
            using (var str = await file.OpenReadAsync())
            using (StreamReader sr = new StreamReader(str.AsStreamForRead(), enc)) {
                var lines = CheckList.FileReadLine(sr);
                res = await this.ReadCSV(lines, dayToDayIndexFunc, updateIdToCircle);
            }

            if (res) {
                await this.ReadTimeFile();
            }

            return res;
        }
        #endregion (OpenCheckList)
        //-------------------------------------------------------------------------------
        #region -[static]FileReadLine ファイルを一行ずつyield return
        //-------------------------------------------------------------------------------
        //
        private static IEnumerable<string> FileReadLine(StreamReader sr)
        {
            while (!sr.EndOfStream) {
                yield return sr.ReadLine();
            }
        }
        #endregion (FileReadLine)

        //-------------------------------------------------------------------------------
        #region -[static]OpenTimeListFile 時間リストファイル
        //-------------------------------------------------------------------------------
        //
        private static async Task<SerializableDictionary<int, CheckListTimeData>> OpenTimeListFile()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;

            var files = await localDir.GetFilesAsync();
            var file = files.FirstOrDefault(sf => sf.Name == CHECKLIST_TIME_LOCAL_FILENAME);
            if (file == null) {
                return new SerializableDictionary<int, CheckListTimeData>();
            }
            else {
                XmlSerializer serializer = new XmlSerializer(typeof(SerializableDictionary<int, CheckListTimeData>));
                try {
                    using (Stream stream = await file.OpenStreamForReadAsync()) {
                        using (XmlReader reader = XmlReader.Create(stream)) {
                            if (serializer.CanDeserialize(reader)) {
                                stream.Seek(0, SeekOrigin.Begin);
                                return (SerializableDictionary<int, CheckListTimeData>)serializer.Deserialize(stream);
                            }
                        }
                    }
                }
                catch (Exception) { }
                return new SerializableDictionary<int, CheckListTimeData>();
            }
        }
        #endregion (OpenTimeListFile)

        //-------------------------------------------------------------------------------
        #region +SaveTimeData 時間データ保存
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> SaveTimeData()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await localDir.CreateFileAsync(CHECKLIST_TIME_LOCAL_FILENAME, CreationCollisionOption.ReplaceExisting);

            XmlSerializer serializer = new XmlSerializer(typeof(SerializableDictionary<int, CheckListTimeData>));
            try {
                using (Stream stream = await file.OpenStreamForWriteAsync())
                using (StreamWriter writer = new StreamWriter(stream)) {
                    serializer.Serialize(writer, _timeData);
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }
        #endregion (SaveTimeData)
        //-------------------------------------------------------------------------------
        #region +SaveCheckList チェックリスト保存
        //-------------------------------------------------------------------------------
        //
        public async Task SaveCheckList(StorageFile file)
        {
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            using (var ostream = stream.GetOutputStreamAt(0))
            using (DataWriter dw = new DataWriter(ostream)) {
                dw.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

                const string SEPARATOR = ",";

                Func<string, string> escape = str => (str.Contains(',') || str.Contains('\n')) ? ("\"" + str + "\"") : str;

                // Header
                {
                    string event_name = string.Format("{0}{1}", CHECKLIST_HEADER_COMICMARKET_STR, _comiketNo);
                    string[] items_header = { "Header", CHECKLIST_HEADER_SIGNATURE, event_name, "utf-8", "ComiketCatalogBrowser for Win8.1" };
                    dw.WriteString(String.Join(SEPARATOR, items_header.Select(escape)));
                    dw.WriteString("\n");
                }

                // Color
                foreach (var color in _colorList) {
                    string color_str = color.B.ToString("X2") + color.G.ToString("X2") + color.R.ToString("X2");
                    string[] items_color = { "Color",
                                             color.Index.ToString(),
                                             color_str, color_str,
                                             color.Description };
                    dw.WriteString(String.Join(SEPARATOR, items_color.Select(escape)));
                    dw.WriteString("\n");
                }

                // Circle
                foreach (var ci in _circleList_d1.Concat(_circleList_d2).Concat(_circleList_d3)) {
                    string[] items_circle = { "Circle",                 // 0
                                              ci.SerialNo.ToString(),
                                              ci.ColorIndex.ToString(),
                                              ci.PageNo.ToString(),
                                              ci.CutIndex.ToString(),
                                              ci.DayOfWeek.ToString(),  // 5
                                              ci.Area.ToString(),
                                              ci.Block.ToString(),
                                              ci.SpaceNo.ToString(),
                                              ci.GenreCode.ToString(),
                                              ci.CircleName,            // 10
                                              ci.CircleKana,
                                              ci.PenName,
                                              ci.BookName,
                                              ci.URL,
                                              ci.Mail,                  // 15
                                              ci.Description,
                                              ci.Memo,
                                              ci.MapX.ToString(),
                                              ci.MapY.ToString(),
                                              ci.Layout.ToString(),     // 20
                                              ci.SpaceNoSub.ToString(),
                                              ci.UpdateInfo,
                                              ci.Url_Circlems,
                                              ci.RSS,
                                              ci.RSSgetInfo             // 25
                                            };
                    dw.WriteString(String.Join(SEPARATOR, items_circle.Select(escape)));
                    dw.WriteString("\n");
                }

                // Unknown
                foreach (var uci in _unknownCircleList) {
                    string[] items_ukcircle = { "Unknown",                  // 0
                                                uci.CircleName,
                                                uci.CircleKana,
                                                uci.PenName,
                                                uci.Memo,
                                                uci.ColorIndex.ToString(),  // 5
                                                uci.BookName,
                                                uci.URL,
                                                uci.Mail,
                                                uci.Description,
                                                uci.UpdateInfo,             // 10
                                                uci.Url_Circlems,
                                                uci.RSS                     // 12
                                               };
                    dw.WriteString(String.Join(SEPARATOR, items_ukcircle.Select(escape)));
                    dw.WriteString("\n");
                }

                await dw.StoreAsync();
                await ostream.FlushAsync();
            }
        }
        #endregion (SaveCheckList)
        //-------------------------------------------------------------------------------
        #region +SaveCheckListToLocal ローカルにチェックリスト保存
        //-------------------------------------------------------------------------------
        //
        public async Task SaveCheckListToLocal()
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await localDir.CreateFileAsync(CHECKLIST_LOCAL_FILENAME, CreationCollisionOption.ReplaceExisting);
            await this.SaveCheckList(file);
        }
        #endregion (SaveCheckListToLocal)

        private ObservableCollection<ColorInfo> _colorList = new ObservableCollection<ColorInfo>();
        private ObservableSortedCollection<CheckFileCircleInfo> _circleList_d1 = new ObservableSortedCollection<CheckFileCircleInfo>();
        private ObservableSortedCollection<CheckFileCircleInfo> _circleList_d2 = new ObservableSortedCollection<CheckFileCircleInfo>();
        private ObservableSortedCollection<CheckFileCircleInfo> _circleList_d3 = new ObservableSortedCollection<CheckFileCircleInfo>();
        private ObservableSortedCollection<CheckFileUnknownCircleInfo> _unknownCircleList = new ObservableSortedCollection<CheckFileUnknownCircleInfo>();

        public ObservableCollection<ColorInfo> ColorInfo { get { return _colorList; } }
        public ObservableSortedCollection<CheckFileCircleInfo> CircleList_Day1 { get { return _circleList_d1; } }
        public ObservableSortedCollection<CheckFileCircleInfo> CircleList_Day2 { get { return _circleList_d2; } }
        public ObservableSortedCollection<CheckFileCircleInfo> CircleList_Day3 { get { return _circleList_d3; } }
        public ObservableSortedCollection<CheckFileUnknownCircleInfo> CircleList_Unknown { get { return _unknownCircleList; } }

        private IComparer<CheckFileCircleInfo> _comparer = null;
        private IComparer<CheckFileUnknownCircleInfo> _comparerUnknown = null;

        public SerializableDictionary<int, CheckListTimeData> TimeData { get { return _timeData; } }
        private SerializableDictionary<int, CheckListTimeData> _timeData = new SerializableDictionary<int, CheckListTimeData>();
        private int _comiketNo;

        //-------------------------------------------------------------------------------
        #region +SetCheckListComparer チェックリストソート用Comparerの設定
        //-------------------------------------------------------------------------------
        //
        public void SetCheckListComparer(IComparer<CheckFileCircleInfo> comparer, IComparer<CheckFileUnknownCircleInfo> comparer_unknown)
        {
            _circleList_d1.Comparer = comparer;
            _circleList_d2.Comparer = comparer;
            _circleList_d3.Comparer = comparer;
            _unknownCircleList.Comparer = comparer_unknown;

            _comparer = comparer;
            _comparerUnknown = comparer_unknown;
        }
        #endregion (SetCheckListComparer)

        //-------------------------------------------------------------------------------
        #region +[static]CheckEncode エンコードのみ確認
        //-------------------------------------------------------------------------------
        //
        public static string CheckEncode(string line)
        {
            string[] sep = line.Split(new char[] { ',' }, StringSplitOptions.None);
            if (sep.Length > 4) { return sep[3]; }

            return null;
        }
        #endregion (CheckEncode)
        //-------------------------------------------------------------------------------
        #region +[static]CheckComiketNo コミケ番号を確認
        //-------------------------------------------------------------------------------
        //
        public static int CheckComiketNo(string line)
        {
            string[] sep = line.Split(new char[] { ',' }, StringSplitOptions.None);
            if (sep.Length > 3) {
                var match = Regex.Match(sep[2], string.Format(@"{0}(\d+)", CHECKLIST_HEADER_COMICMARKET_STR));
                if (match.Success) {
                    return int.Parse(match.Groups[1].Value);
                }
            }

            return -1;
        }
        #endregion (CheckComiketNo)

        //-------------------------------------------------------------------------------
        #region +ColorIndexToColorFunc 色インデックスから色
        //-------------------------------------------------------------------------------
        //
        public Color ColorIndexToColorFunc(int colorIndex)
        {
            var cinfo = _colorList.Where(ci => ci.Index == colorIndex)
                                  .FirstOrDefault();
            if (cinfo == null) {
                return new Color() { A = 0 };
            }
            else {
                return new Color() { R = cinfo.R, G = cinfo.G, B = cinfo.B, A = 255 };
            }
        }
        #endregion (ColorIndexToColorFunc)
        //-------------------------------------------------------------------------------
        #region +ColorIndexToColorDescriptionFunc 色インデックスから色の説明
        //-------------------------------------------------------------------------------
        //
        public string ColorIndexToColorDescriptionFunc(int colorIndex)
        {
            var cinfo = _colorList.Where(ci => ci.Index == colorIndex)
                                 .FirstOrDefault();
            return (cinfo == null) ? "" : cinfo.Description;
        }
        #endregion (ColorIndexToColorDescriptionFunc)

        //-------------------------------------------------------------------------------
        #region -ReadCSV csvファイル(行ごとの文字列配列)から初期化
        //-------------------------------------------------------------------------------
        //
        private async Task<bool> ReadCSV(IEnumerable<string> lines, Func<char, Task<int>> dayToDayIndexFunc, Func<int, Task<ComiketCircleAndLayout>> updateIdToCircle)
        {
            char[] SEPARATOR = new char[] { ',' };
            _colorList.Clear();
            _circleList_d1.Clear();
            _circleList_d2.Clear();
            _circleList_d3.Clear();
            _unknownCircleList.Clear();

            bool inComma = false;
            List<string> continueList = null;
            foreach (var line in lines) {
                if (line.Length == 0 && continueList != null) {
                    continueList[continueList.Count - 1] = continueList[continueList.Count - 1] + "\n";
                    continue;
                }

                var elements = line.Split(SEPARATOR, StringSplitOptions.None);

                List<string> elementsList = new List<string>();
                StringBuilder sb = (inComma) ? new StringBuilder() : null;
                for (int i = 0; i < elements.Length; ++i) {
                    string curr = elements[i];
                    if (curr.Length > 0) {

                        //bool doubleQuotationExists = curr.Contains("\"\"");
                        int commaCount = curr.Count(c => c == '"');
                        bool commaExists = (commaCount % 2 == 1);

                        if (commaExists) {
                            if (!inComma) {
                                sb = new StringBuilder();
                                sb.Append(curr.Replace("\"", ""));
                                inComma = true;
                            }
                            else {
                                sb.Append(curr.Replace("\"", ""));
                                elementsList.Add(sb.ToString());
                                sb = null;
                                inComma = false;
                            }
                        }
                        else if (inComma) { sb.Append(curr); }
                        else { elementsList.Add(curr); }
                    }
                    else {
                        elementsList.Add(curr);
                    }
                }
                if (sb != null) { elementsList.Add(sb.ToString()); }
                elements = elementsList.ToArray();

                if (continueList != null) {
                    continueList[continueList.Count - 1] = continueList[continueList.Count - 1] + "\n" + elements[0];
                    for (int i = 1; i < elements.Length; ++i) { continueList.Add(elements[i]); }
                    elements = continueList.ToArray();
                    continueList = null;
                }

                if (inComma) {
                    continueList = new List<string>();
                    continueList.AddRange(elements);
                    continue;
                }

                switch (elements[0]) {
                    case "Header":

                        break;
                    case "Color": {
                            string colorStr = elements[2];
                            var ci = new ColorInfo()
                            {
                                Index = int.Parse(elements[1]),
                                R = byte.Parse(colorStr.Substring(4, 2), System.Globalization.NumberStyles.HexNumber),
                                G = byte.Parse(colorStr.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                                B = byte.Parse(colorStr.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                                Description = elements[4]
                            };
                            _colorList.Add(ci);
                        }
                        break;
                    case "Circle": {
                            try {

                                CheckFileCircleInfo ci = new CheckFileCircleInfo()
                                {
                                    SerialNo = int.Parse(elements[1]),
                                    ColorIndex = int.Parse(elements[2]),
                                    PageNo = int.Parse(elements[3]),
                                    CutIndex = int.Parse(elements[4]),
                                    DayOfWeek = elements[5][0],
                                    Area = elements[6][0],
                                    Block = elements[7][0],
                                    SpaceNo = int.Parse(elements[8]),
                                    GenreCode = int.Parse(elements[9]),
                                    CircleName = elements[10],
                                    CircleKana = elements[11],
                                    PenName = elements[12],
                                    BookName = elements[13],
                                    URL = elements[14],
                                    Mail = elements[15],
                                    Description = elements[16],
                                    Memo = elements[17],
                                   

                                    IndexToColorFunc = ColorIndexToColorFunc,
                                    IndexToColorDescriptionFunc = ColorIndexToColorDescriptionFunc
                                };

                                if (elements.Length > 23 && !string.IsNullOrWhiteSpace(elements[18])) {
                                    ci.MapX = int.Parse(elements[18]);
                                    ci.MapY = int.Parse(elements[19]);
                                    ci.Layout = int.Parse(elements[20]);
                                    ci.SpaceNoSub = int.Parse(elements[21]);

                                    ci.UpdateInfo = elements[22];
                                    ci.Url_Circlems = elements[23];
                                    ci.RSS = (elements.Length > 24) ? elements[24] : "";
                                    ci.RSSgetInfo = (elements.Length > 25) ? elements[25] : "";
                                }
                                else {
                                    int serialNo = int.Parse(elements[1]);
                                    ComiketCircleAndLayout ccl = await updateIdToCircle(serialNo);

                                    ci.MapX = ccl.xpos;
                                    ci.MapY = ccl.ypos;
                                    ci.Layout = ccl.layout;
                                    ci.SpaceNoSub = ccl.spaceNoSub;

                                    ci.UpdateInfo = "";
                                    ci.Url_Circlems = ccl.circlems;
                                    ci.RSS = ccl.rss;
                                    ci.RSSgetInfo = "";
                                }

                                switch (await dayToDayIndexFunc(ci.DayOfWeek)) {
                                    case 1: _circleList_d1.Add(ci); break;
                                    case 2: _circleList_d2.Add(ci); break;
                                    case 3: _circleList_d3.Add(ci); break;
                                }
                            }
                            catch (Exception) {
                                try {
                                    var ci = new CheckFileUnknownCircleInfo()
                                    {
                                        CircleName = elements[10],
                                        CircleKana = elements[11],
                                        PenName = elements[12],
                                        Memo = elements[17],
                                        ColorIndex = int.Parse(elements[2]),
                                        BookName = elements[13],
                                        URL = elements[14],
                                        Mail = elements[15],
                                        Description = elements[16],
                                        UpdateInfo = elements[22],
                                        Url_Circlems = elements[23],
                                        RSS = (elements.Length > 24) ? elements[24] : "",

                                        IndexToColorFunc = ColorIndexToColorFunc,
                                        IndexToColorDescriptionFunc = ColorIndexToColorDescriptionFunc
                                    };
                                    _unknownCircleList.Add(ci);
                                }
                                catch (Exception ex) { throw ex; }
                            }

                        }
                        break;
                    case "UnKnown": {
                            CheckFileUnknownCircleInfo ci;
                            try {
                                ci = new CheckFileUnknownCircleInfo()
                                {
                                    CircleName = elements[1],
                                    CircleKana = elements[2],
                                    PenName = elements[3],
                                    Memo = elements[4],
                                    ColorIndex = int.Parse(elements[5]),
                                    BookName = elements[6],
                                    URL = elements[7],
                                    Mail = elements[8],
                                    Description = elements[9],
                                    UpdateInfo = elements[10],
                                    Url_Circlems = elements[11],
                                    RSS = elements[12],

                                    IndexToColorFunc = ColorIndexToColorFunc,
                                    IndexToColorDescriptionFunc = ColorIndexToColorDescriptionFunc
                                };
                            }
                            catch (Exception ex) { throw ex; }
                            _unknownCircleList.Add(ci);
                        }
                        break;
                    default:
                        break;
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs("ColorInfo"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Unknown"));
            return true;
        }
        #endregion (ReadCSV)
        //-------------------------------------------------------------------------------
        #region -ReadTimeFile 時間ファイルをローカルから読み込み
        //-------------------------------------------------------------------------------
        //
        private async Task ReadTimeFile()
        {
            _timeData = await CheckList.OpenTimeListFile();
        }
        #endregion (ReadTimeFile)

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

        //-------------------------------------------------------------------------------
        #region -SetDefaultColors デフォルト色追加
        //-------------------------------------------------------------------------------
        private static readonly List<ColorInfo> DEFAULT_COLORS = new List<ColorInfo>() {
            new ColorInfo() { Index = 1, R = 255, G = 148, B = 74, Description = "" },
            new ColorInfo() { Index = 2, R = 255, G = 0, B = 255, Description = "" },
            new ColorInfo() { Index = 3, R = 255, G = 247, B = 0, Description = "" },
            new ColorInfo() { Index = 4, R = 0, G = 181, B = 74, Description = "" },
            new ColorInfo() { Index = 5, R = 0, G = 181, B = 255, Description = "" },
            new ColorInfo() { Index = 6, R = 156, G = 82, B = 156, Description = "" },
            new ColorInfo() { Index = 7, R = 0, G = 0, B = 255, Description = "" },
            new ColorInfo() { Index = 8, R = 0, G = 255, B = 0, Description = "" },
            new ColorInfo() { Index = 9, R = 255, G = 0, B = 0, Description = "" }
        };
        //
        private void SetDefaultColors()
        {
            _colorList.Clear();
            foreach (var color in CheckList.DEFAULT_COLORS) {
                var cinfo = new ColorInfo() { Index = color.Index, R = color.R, G = color.G, B = color.B, Description = color.Description };
                _colorList.Add(cinfo);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("ColorInfo"));
        }
        #endregion (SetDefaultColors)

        //-------------------------------------------------------------------------------
        #region +ClearCircles サークル削除
        //-------------------------------------------------------------------------------
        //
        public void ClearCircles()
        {
            _circleList_d1.Clear();
            _circleList_d2.Clear();
            _circleList_d3.Clear();
            _unknownCircleList.Clear();

            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3"));
            OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Unknown"));
        }
        #endregion (+ClearCircles)

        //-------------------------------------------------------------------------------
        #region +AddCircle サークル追加
        //-------------------------------------------------------------------------------
        //
        public void AddCircle(CheckFileCircleInfo cinfo, int day)
        {
            cinfo.IndexToColorFunc = ColorIndexToColorFunc;
            cinfo.IndexToColorDescriptionFunc = ColorIndexToColorDescriptionFunc;
            switch (day) {
                case 1:
                    _circleList_d1.Add(cinfo);
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1"));
                    break;
                case 2:
                    _circleList_d2.Add(cinfo);
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2"));
                    break;
                case 3:
                    _circleList_d3.Add(cinfo);
                    OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3"));
                    break;
                default: return;
            }
        }
        #endregion (+AddCircle)

        //-------------------------------------------------------------------------------
        #region +ChangeColor アイテムの色の変更
        //-------------------------------------------------------------------------------
        //
        public void ChangeColor(CheckFileCircleInfo checkListItem, int colorIndex)
        {
            var tuple = GetListAndIndexOfCheckFileCircleInfo(checkListItem);
            if (tuple == null) { return; }
            var list = tuple.Item1;
            var index = tuple.Item2;

            var c = list[index];
            c.ColorIndex = colorIndex;

            list.Refresh(index);

            if (list == _circleList_d1) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1"));
            }
            else if (list == _circleList_d2) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2"));
            }
            else if (list == _circleList_d3) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3"));
            }
        }
        public void ChangeColor(CheckFileUnknownCircleInfo checkListItem, int colorIndex)
        {
            int index = _unknownCircleList.IndexOf(checkListItem);
            if (index >= 0) {
                var c = _unknownCircleList[index];
                c.ColorIndex = colorIndex;
                _unknownCircleList[index] = c;
            }
        }
        #endregion (+ChangeColor)

        //-------------------------------------------------------------------------------
        #region +RefreshItem 表示のリフレッシュ(by observable collection)
        //-------------------------------------------------------------------------------
        //
        public void RefreshItem(CheckFileCircleInfo checkListItem)
        {
            var tuple = GetListAndIndexOfCheckFileCircleInfo(checkListItem);
            if (tuple == null) { return; }
            var list = tuple.Item1;
            var index = tuple.Item2;

            list.Refresh(index);

            if (list == _circleList_d1) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day1"));
            }
            else if (list == _circleList_d2) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day2"));
            }
            else if (list == _circleList_d3) {
                OnPropertyChanged(new PropertyChangedEventArgs("CircleList_Day3"));
            }
        }
        #endregion (RefreshItem)

        //-------------------------------------------------------------------------------
        #region -RemoveItem アイテムの削除
        //-------------------------------------------------------------------------------
        //
        public void RemoveItem(CheckFileCircleInfo item)
        {
            var tuple = GetListAndIndexOfCheckFileCircleInfo(item);
            if (tuple == null) { return; }
            var list = tuple.Item1;
            var index = tuple.Item2;

            list.RemoveAt(index);
        }
        public void RemoveItem(CheckFileUnknownCircleInfo item)
        {
            int index = _unknownCircleList.IndexOf(item);
            if (index >= 0) {
                _unknownCircleList.RemoveAt(index);
            }
        }
        #endregion (RemoveItem)

        //-------------------------------------------------------------------------------
        #region +GetListAndIndexOfCheckFileCircleInfo CheckFileCircleInfo -> ObservableCollection<CheckFileCircleInfo>
        //-------------------------------------------------------------------------------
        //
        public Tuple<ObservableSortedCollection<CheckFileCircleInfo>, int> GetListAndIndexOfCheckFileCircleInfo(CheckFileCircleInfo item)
        {
            int index1 = _circleList_d1.IndexOf(item);
            if (index1 >= 0) {
                return Tuple.Create(_circleList_d1, index1);
            }
            int index2 = _circleList_d2.IndexOf(item);
            if (index2 >= 0) {
                return Tuple.Create(_circleList_d2, index2);
            }
            int index3 = _circleList_d3.IndexOf(item);
            if (index3 >= 0) {
                return Tuple.Create(_circleList_d3, index3);
            }
            return null;
        }
        #endregion (GetListAndIndexOfCheckFileCircleInfo)

        //-------------------------------------------------------------------------------
        #region +GetCircleInfo サークル情報取得
        //-------------------------------------------------------------------------------
        //
        public CheckFileCircleInfo GetCircleInfo(int serialNo)
        {
            var item1 = _circleList_d1.FirstOrDefault(cfci => cfci.SerialNo == serialNo);
            if (item1 != null) { return item1; }
            var item2 = _circleList_d2.FirstOrDefault(cfci => cfci.SerialNo == serialNo);
            if (item2 != null) { return item2; }
            var item3 = _circleList_d3.FirstOrDefault(cfci => cfci.SerialNo == serialNo);
            if (item3 != null) { return item3; }
            return null;
        }
        #endregion (GetCircleInfo)

        //-------------------------------------------------------------------------------
        #region +GetCircleBrush サークル色取得(ない場合は白)
        //-------------------------------------------------------------------------------
        //
        public Brush GetCircleBrush(int serialNo)
        {
            var info = GetCircleInfo(serialNo);
            return (info == null) ? new SolidColorBrush() : info.Color;
        }
        #endregion (GetCircleInfo)
    }

    //-------------------------------------------------------------------------------
    #region Classes for CheckList
    //-------------------------------------------------------------------------------
    public class ColorInfo
    {
        public int Index { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string Description { get; set; }

        public Brush Color { get { return new SolidColorBrush(Windows.UI.Color.FromArgb(255, R, G, B)); } }
        public string ColorDescription { get { return string.Format("色{0}{1}", Index, (Description.Length > 0) ? string.Format(" : {0}", Description) : ""); } }
    }

    public class CheckFileCircleInfo
    {
        //2: サークルシリアル番号  ※必須項目
        public int SerialNo { get; set; } // equivalent to updateId
        //3: チェックカラー番号  ※必須項目
        public int ColorIndex { get; set; } // 0-9

        // from circle info
        //4: ページ番号
        public int PageNo { get; set; }
        //5: カットインデックス
        public int CutIndex { get; set; }
        //6: 参加曜日
        public char DayOfWeek { get; set; }
        //7: 配置地区
        public char Area { get; set; }
        //8: ブロック名
        public char Block { get; set; }
        //9: スペース番号
        public int SpaceNo { get; set; }
        //10: ジャンルコード
        public int GenreCode { get; set; }
        //11: サークル名
        public string CircleName { get; set; }
        //12: サークル名の読み仮名
        public string CircleKana { get; set; }
        //13: 執筆者名   
        public string PenName { get; set; }
        //14: 発行誌名   
        public string BookName { get; set; }
        //15: ＵＲＬ   
        public string URL { get; set; }
        //16: メールアドレス   
        public string Mail { get; set; }
        //17: 補足説明 半角で最大4000文字。 
        public string Description { get; set; }

        // from user input
        //18: メモ
        public string Memo { get; set; }

        // from map data
        //19: 配置図のX座標 マップ情報ファイルの X座標と同じ値が入る
        public int MapX { get; set; }
        //20: 配置図のY座標 マップ情報ファイルの Y座標と同じ値が入る
        public int MapY { get; set; }
        //21: 配置図のテーブルレイアウト情報 マップ情報ファイルのレイアウトと同じ値が入る
        public int Layout { get; set; }
        //22: スペース配置 aの場合:0 bの場合:1 
        public int SpaceNoSub { get; set; }

        //23: 更新情報
        public string UpdateInfo { get; set; }
        //24: Circle.ms のサークルページのURL
        public string Url_Circlems { get; set; }
        //25: RSS
        public string RSS { get; set; }
        //26: RSS取得情報
        public string RSSgetInfo { get; set; }

        public Func<int, Color> IndexToColorFunc { get; set; }
        public Func<int, string> IndexToColorDescriptionFunc { get; set; }

        public Brush Color { get { return new SolidColorBrush(IndexToColorFunc(ColorIndex)); } }
        public string ColorDescription { get { return IndexToColorDescriptionFunc(ColorIndex); } }
        public string CircleInfo { get { return string.Format("{0} {1}{2:D2}{3} {4}", Area, Block, SpaceNo, (SpaceNoSub == 0) ? 'a' : 'b', CircleName); } }
        public string SpaceStr { get { return string.Format("{0} {1}{2:D2}{3}", Area, Block, SpaceNo, (SpaceNoSub == 0) ? 'a' : 'b'); } }
    }

    public class CheckFileUnknownCircleInfo
    {
        //2: サークル名
        public string CircleName { get; set; }
        //3: サークル名の読み仮名
        public string CircleKana { get; set; }
        //4: 執筆者名
        public string PenName { get; set; }

        //5: メモ (CM66では未記入。CM67 から対応)
        public string Memo { get; set; }

        //6: チェックカラー番号 (CM67から追加)
        public int ColorIndex { get; set; } // 0-9

        //7: 発行誌名
        public string BookName { get; set; }
        //8: ＵＲＬ   
        public string URL { get; set; }
        //9: メールアドレス
        public string Mail { get; set; }
        //10: 補足説明 半角で最大4000文字。 
        public string Description { get; set; }


        //11: 更新情報
        public string UpdateInfo { get; set; }
        //12: Circle.ms のサークルページのURL
        public string Url_Circlems { get; set; }
        //13: RSS
        public string RSS { get; set; }

        public Func<int, Color> IndexToColorFunc { get; set; }
        public Func<int, string> IndexToColorDescriptionFunc { get; set; }

        public Brush Color { get { return new SolidColorBrush(IndexToColorFunc(ColorIndex)); } }
        public string ColorDescription { get { return IndexToColorDescriptionFunc(ColorIndex); } }
        public string CircleInfo { get { return CircleName; } }
    }
    //-------------------------------------------------------------------------------
    #endregion (Classes for CheckList)

    //-------------------------------------------------------------------------------
    #region CheckListTime
    //-------------------------------------------------------------------------------
    public class CheckListTimeData
    {
        public string Memo;
        public DateTime? Time_startLine;
        public DateTime? Time_bought;
    }
    //-------------------------------------------------------------------------------
    #endregion (CheckListTime)
}