using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ComicStarViewer {
    public class WebCatalogData {
        private string _accesstoken = "";

        private readonly static string CLIENT_ID = "";
        private readonly static string CLIENT_SECRET = "";
        private readonly static string REDIRECT_URL = @"https://circle.ms/";

        //-------------------------------------------------------------------------------
        #region (Constants)URL
        //-------------------------------------------------------------------------------
        private readonly static string URL_HOST_AUTH = @"https://auth1-sandbox.circle.ms/";
        private readonly static string URL_HOST_SERVICE = @"https://api1-sandbox.circle.ms/";

        /// <summary>認証用API</summary>
        private readonly static string URL_AUTHORIZE = URL_HOST_AUTH + @"OAuth2/?response_type=code&state=xyz&scope=circle_read%20circle_write%20favorite_read%20favorite_write";
        /// <summary>アクセストークン取得(POST)</summary>
        private readonly static string URL_GETTOKEN = URL_HOST_AUTH + @"OAuth2/Token";

        /// <summary>ユーザー情報取得</summary>
        private readonly static string URL_GETUSERINFO = URL_HOST_SERVICE + @"User/Info/";

        /// <summary>イベントリスト取得</summary>
        private readonly static string URL_GETEVENTLIST = URL_HOST_SERVICE + @"WebCatalog/GetEventList/";

        ///<summary>初期データ取得</summary>
        private readonly static string URL_ALLDATA = URL_HOST_SERVICE + @"CatalogBase/All/";

        /// <summary>サークル一覧(GET)</summary>
        private readonly static string URL_CIRCLE_LIST = URL_HOST_SERVICE + @"WebCatalog/QueryCircle/";
        /// <summary>サークル詳細(GET)</summary>
        private readonly static string URL_CIRCLE_DATA = URL_HOST_SERVICE + @"WebCatalog/GetCircle/";
        /// <summary>サークル頒布物(GET)</summary>
        private readonly static string URL_CIRCLE_BOOKS = URL_HOST_SERVICE + @"WebCatalog/QueryBook/";


        /// <summary> お気に入り管理(更新:PUT 追加:POST 削除:DELETE)</summary>
        private readonly static string URL_FAVORITE = URL_HOST_SERVICE + @"Readers/Favorite";
        /// <summary>お気に入りリスト(GET)</summary>
        private readonly static string URL_FAVORITE_LIST = URL_HOST_SERVICE + @"Readers/FavoriteCircles/";

        //"Readers/FavoriteBooks/ "
        //-------------------------------------------------------------------------------
        #endregion (URL_constants)

        //-------------------------------------------------------------------------------
        #region Authorized プロパティ：認証済みかどうか
        //-------------------------------------------------------------------------------
        /// <summary>
        /// 
        /// </summary>
        public bool Authorized {
            get { return !string.IsNullOrEmpty(_accesstoken); }
        }
        #endregion (Authorized)

        //-------------------------------------------------------------------------------
        #region +SetAccessToken 保存済みアクセストークンをセット
        //-------------------------------------------------------------------------------
        //
        public void SetAccessToken(string accessToken) {
            _accesstoken = accessToken;
        }
        #endregion (SetAccessToken)

        //===============================================================================
        #region -[async]GetJsonFromAPI API経由でデータを取りに行く
        //-------------------------------------------------------------------------------
        //
        private async Task<string> GetJsonFromAPI(string url, string method, StringBuilder contents = null) {

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = method;
            if (contents != null) {
                request.ContentType = "application/x-www-form-urlencoded; charset=ASCII";
                using (var writer = new StreamWriter(await request.GetRequestStreamAsync(), Encoding.GetEncoding("ASCII"))) {
                    writer.Write(contents.ToString());
                }
            }

            // アクセストークンを取得する
            WebResponse res = await request.GetResponseAsync();
            Stream resStream = res.GetResponseStream();
            StreamReader sr = new StreamReader(resStream, Encoding.UTF8);
            string jsonStr = sr.ReadToEnd();

            return jsonStr;
        }
        #endregion (GetJsonFromAPI)

        //===============================================================================
        #region +GetAuthorizeURL 認証のためのURL取得
        //-------------------------------------------------------------------------------
        //
        public string GetAuthorizeURL() {
            return URL_AUTHORIZE + "&client_id=" + WebCatalogData.CLIENT_ID + "&redirect_uri=" + WebCatalogData.REDIRECT_URL;
        }
        #endregion (GetAuthorizeURL)
        //-------------------------------------------------------------------------------
        #region +[async]Authorize 認証して手に入れたコードからAccessTokenを取得し設定
        //-------------------------------------------------------------------------------
        //
        public async Task<bool> Authorize(string code) {
            if (code == "") { return false; }

            // circle.msに認証コードを送る
            StringBuilder bld = new StringBuilder();
            bld.Append("grant_type=authorization_code&client_id=" + WebCatalogData.CLIENT_ID + "&client_secret=" + WebCatalogData.CLIENT_SECRET + "&code=" + code);

            // アクセストークン取得
            string jsonStr = await GetJsonFromAPI(WebCatalogData.URL_GETTOKEN, "POST", bld);

            // jsonからAccessTokenを読み取り
            var jobj = JObject.Parse(jsonStr);
            JToken token;
            if (jobj.TryGetValue("access_token", out token)) {
                _accesstoken = token.ToString();
                return true;
            }
            else { return false; }

        }
        #endregion (Authorize)

        //-------------------------------------------------------------------------------
        #region +[async]GetUserInfo ユーザー情報取得
        //-------------------------------------------------------------------------------
        //
        public async Task<UserData> GetUserInfo() {
            string url = WebCatalogData.URL_GETUSERINFO + "?access_token=" + this._accesstoken;
            string jsonStr = await GetJsonFromAPI(url, "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<UserData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetUserInfo)

        //-------------------------------------------------------------------------------
        #region +[async]GetEventList イベント情報取得
        //-------------------------------------------------------------------------------
        //
        public async Task<EventList> GetEventList() {
            string url = WebCatalogData.URL_GETEVENTLIST + "?access_token=" + this._accesstoken;
            string jsonStr = await GetJsonFromAPI(url, "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<EventList>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetEventList)

        //===============================================================================
        #region +[async]GetAllData 初期データ取得
        //-------------------------------------------------------------------------------
        //
        public async Task<AllData> GetAllData(int event_id) {
            string url = WebCatalogData.URL_ALLDATA + "?event_id=" + event_id.ToString() + "&access_token=" + this._accesstoken;

            string jsonStr = await GetJsonFromAPI(url, "GET");

            // jsonからAccessTokenを読み取り
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<AllData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetInitialData)
        //-------------------------------------------------------------------------------
        #region +[async]GetCircleList サークル一覧取得
        //-------------------------------------------------------------------------------
        //
        public async Task<CircleList> GetCircleList(int eventID, SortType sortType, string circle_name = "", string genre = "", string floor = "", int page = -1) {
            StringBuilder sb = new StringBuilder();
            sb.Append(WebCatalogData.URL_CIRCLE_LIST);
            sb.Append("?event_id=" + eventID);
            sb.Append("&access_token=" + _accesstoken);
            if (!string.IsNullOrEmpty(circle_name)) { sb.Append("&circle_name=" + circle_name); }
            if (!string.IsNullOrEmpty(genre)) { sb.Append("&genre=" + genre); }
            if (!string.IsNullOrEmpty(floor)) { sb.Append("&floor=" + floor); }
            sb.Append("&sort=" + ((int)sortType).ToString());
            if (page > 0) { sb.Append("&page=" + page.ToString()); }

            string jsonStr = await GetJsonFromAPI(sb.ToString(), "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleList>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetCircleList)
        //-------------------------------------------------------------------------------
        #region +[async]GetCircleData サークルデータ取得
        //-------------------------------------------------------------------------------
        //
        public async Task<CircleData> GetCircleData(long wcid) {
            StringBuilder sb = new StringBuilder();
            sb.Append(WebCatalogData.URL_CIRCLE_DATA);
            sb.Append("?wcid=" + wcid.ToString());
            sb.Append("&access_token=" + _accesstoken);

            string jsonStr = await GetJsonFromAPI(sb.ToString(), "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetCircleData)
        //-------------------------------------------------------------------------------
		#region +[async]GetCircleBooks サークル頒布物取得
		//-------------------------------------------------------------------------------
		//
		public async Task<CircleBookList> GetCircleBooks(int eventID, SortType sortType, string circle_name = "", string work_name = "", string work_word = "", string genre = "", string floor = "", int page = -1) {
            StringBuilder sb = new StringBuilder();
            sb.Append(WebCatalogData.URL_CIRCLE_BOOKS);
            sb.Append("?event_id=" + eventID);
            sb.Append("&access_token=" + _accesstoken);
            if (!string.IsNullOrEmpty(circle_name)) { sb.Append("&circle_name=" + circle_name); }
            if (!string.IsNullOrEmpty(work_name)) { sb.Append("&work_name=" + work_name); }
            if (!string.IsNullOrEmpty(work_word)) { sb.Append("&work_word=" + work_word); }
            if (!string.IsNullOrEmpty(genre)) { sb.Append("&genre=" + genre); }
            if (!string.IsNullOrEmpty(floor)) { sb.Append("&floor=" + floor); }
            sb.Append("&sort=" + ((int)sortType).ToString());
            if (page > 0) { sb.Append("&page=" + page.ToString()); }

            string jsonStr = await GetJsonFromAPI(sb.ToString(), "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleBookList>(response);

                return data;
            }
            else { return null; }
		}
		#endregion (GetCircleBooks)

        //===============================================================================
        #region +[async]GetFavoriteList お気に入り一覧取得
        //-------------------------------------------------------------------------------
        //
        public async Task<FavoriteList> GetFavoriteList(int eventID, SortType sortType, string circle_name = "", string genre = "", string floor = "", int page = -1) {
            StringBuilder sb = new StringBuilder();
            sb.Append(WebCatalogData.URL_FAVORITE_LIST);
            sb.Append("?event_id=" + eventID);
            sb.Append("&access_token=" + _accesstoken);
            if (!string.IsNullOrEmpty(circle_name)) { sb.Append("&circle_name=" + circle_name); }
            if (!string.IsNullOrEmpty(genre)) { sb.Append("&genre=" + genre); }
            if (!string.IsNullOrEmpty(floor)) { sb.Append("&floor=" + floor); }
            sb.Append("&sort=" + ((int)sortType).ToString());
            if (page > 0) { sb.Append("&page=" + page.ToString()); }

            string jsonStr = await GetJsonFromAPI(sb.ToString(), "GET");
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<FavoriteList>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (GetFavoriteList)
        //-------------------------------------------------------------------------------
        #region +[async]AddFavorite お気に入り追加
        //-------------------------------------------------------------------------------
        //
        public async Task<CircleData> AddFavorite(long wcid, int color, string memo = "", string free = "") {
            if (color <= 0 || color >= 10) { throw new ArgumentException("Invalid range : color = " + color.ToString()); }

            StringBuilder sb = new StringBuilder();
            sb.Append("wcid=" + wcid.ToString());
            sb.Append("&access_token=" + _accesstoken);
            if (!string.IsNullOrEmpty(memo)) { sb.Append("&memo=" + memo); }
            if (!string.IsNullOrEmpty(free)) { sb.Append("&free=" + free); }

            string jsonStr = await GetJsonFromAPI(WebCatalogData.URL_CIRCLE_DATA, "POST", sb);
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (AddFavorite)
        //-------------------------------------------------------------------------------
        #region +[async]UpdateFavorite お気に入り更新
        //-------------------------------------------------------------------------------
        //
        public async Task<CircleData> UpdateFavorite(long wcid, int color, string memo = "", string free = "") {
            if (color <= 0 || color >= 10) { throw new ArgumentException("Invalid range : color = " + color.ToString()); }

            StringBuilder sb = new StringBuilder();
            sb.Append("wcid=" + wcid.ToString());
            sb.Append("&access_token=" + _accesstoken);
            if (!string.IsNullOrEmpty(memo)) { sb.Append("&memo=" + memo); }
            if (!string.IsNullOrEmpty(free)) { sb.Append("&free=" + free); }

            string jsonStr = await GetJsonFromAPI(WebCatalogData.URL_CIRCLE_DATA, "POST", sb);
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (UpdateFavorite)
        //-------------------------------------------------------------------------------
        #region +[async]DeleteFavorite お気に入り削除
        //-------------------------------------------------------------------------------
        //
        public async Task<CircleData> DeleteFavorite(long wcid) {
            StringBuilder sb = new StringBuilder();
            sb.Append("wcid=" + wcid.ToString());
            sb.Append("&access_token=" + _accesstoken);

            string jsonStr = await GetJsonFromAPI(WebCatalogData.URL_CIRCLE_DATA, "DELETE", sb);
            var jobj = JObject.Parse(jsonStr);
            string status = jobj.Value<string>("status");
            if (status == "success") {
                string response = jobj["response"].ToString();
                var data = JsonConvert.DeserializeObject<CircleData>(response);

                return data;
            }
            else { return null; }
        }
        #endregion (DeleteFavorite)
    }

    //-------------------------------------------------------------------------------
    #region +[class]UserData
    //-------------------------------------------------------------------------------
    public class UserData {
        public long pid;
        public int r18;
        public string nickname;
    }
    //-------------------------------------------------------------------------------
    #endregion (UserData)

    //-------------------------------------------------------------------------------
    #region +[class]EventList
    //-------------------------------------------------------------------------------
    public class EventList {
        public class EventItem {
            public int EventId;
            public int EventNo;
        }
        public EventItem[] list;
        public int LatestEventId;
        public int LatestEventNo;

        public EventItem getReallyLatestEventItem() {
            var max = list.Max(ei => ei.EventNo);
            return list.First(ei => ei.EventNo == max);
        }
    }
    //-------------------------------------------------------------------------------
    #endregion (EventList)

    //-------------------------------------------------------------------------------
    #region SortType 列挙体
    //-------------------------------------------------------------------------------
    public enum SortType {
        /// <summary>新着順</summary>
        New = 0,
        /// <summary>配置順</summary>
        Place = 1
    }
    //-------------------------------------------------------------------------------
    #endregion (SortType)
    //-------------------------------------------------------------------------------
    #region +[class]AllData
    //-------------------------------------------------------------------------------
    public class AllData {
        public class Values {
            public string textdb_sqlite2_url;
            public string textdb_sqlite3_url;
            public string imagedb1_url;
            public string imagedb2_url;
            public string textdb_sqlite2_zip_url;
            public string textdb_sqlite3_zip_url;
            public string imagedb1_zip_url;
            public string imagedb2_zip_url;
        }
        public Values url;
        public Values md5;
        public string updatedate;
    }
    //-------------------------------------------------------------------------------
    #endregion (AllData)

    //-------------------------------------------------------------------------------
    #region +[class]CircleList
    //-------------------------------------------------------------------------------
    public class CircleList {
        public int count;
        public int maxcount;
        public CircleOutline[] list;
    }
    //-------------------------------------------------------------------------------
    #endregion (CircleList)
    //-------------------------------------------------------------------------------
    #region +[class]CircleData
    //-------------------------------------------------------------------------------
    public class CircleData {
        public CircleOutline circle;
        public FavoriteData favorite;
    }
    //-------------------------------------------------------------------------------
    #endregion (CircleData)
    //-------------------------------------------------------------------------------
    #region +[class]CircleOutline
    //-------------------------------------------------------------------------------
    public class CircleOutline {
        public class OnlineStoreInfo {
            public string name;
            public string link;
        }
        public long wcid;
        public string name;
        public string name_kana;
        public long circlemsId;
        public string cut_url;
        public string cut_web_url;
        public string cut_base_url;
        public int genre;
        public string pixiv_url;
        public string twitter_url;
        public string tag;
        public string description;
        public OnlineStoreInfo[] onlinestore;
        public long updateId;
        public string update_date;
    }
    //-------------------------------------------------------------------------------
    #endregion (CircleOutline)
    //-------------------------------------------------------------------------------
    #region +[class]CircleBookList
	//-------------------------------------------------------------------------------
    public class CircleBookList {
        public int count;
        public int maxcount;
        public BookData[] list;
    }
    //-------------------------------------------------------------------------------
	#endregion (CircleBookList)
    //-------------------------------------------------------------------------------
    #region +[class]BookData
    //-------------------------------------------------------------------------------
    public class BookData {
        public string work_id;
        public long wcid;
        public int num;
        public string name;
        public string size;
        public int page;
        public string genre;
        public string dist_date;
        public int new_book;
        public string image_url;
        public string introduction;
        public string update_date;
        public int r18;
    }
    //-------------------------------------------------------------------------------
    #endregion (BookData)

    //-------------------------------------------------------------------------------
    #region +[class]FavoriteList
    //-------------------------------------------------------------------------------
    public class FavoriteList {
        public CircleOutline[] circle;
        public FavoriteData[] favorite;
    }
    //-------------------------------------------------------------------------------
    #endregion (FavoriteList)
    //-------------------------------------------------------------------------------
    #region +[class]FavoriteData
	//-------------------------------------------------------------------------------
    public class FavoriteData {
        public long wcid;
        public string circle_name;
        public int color;
        public string memo;
        public string free;
        public string update_date;
    }
    //-------------------------------------------------------------------------------
	#endregion (FavoriteData)

    //-------------------------------------------------------------------------------
    #region +[class]ErrorInfo
    //-------------------------------------------------------------------------------
    public class ErrorInfo {
        string error;
        string error_description;
        string error_url;
    }
    //-------------------------------------------------------------------------------
    #endregion (ErrorInfo)
}
