using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ComicStarViewer {
    public static class ComiketUtil {

        // ブロックの順番比較用
        public class BlockComparer : IComparer<char> {
            private static BlockComparer _instance = new BlockComparer();
            public static BlockComparer GetInstance() { return _instance; }
            private BlockComparer() { }
            public int Compare(char x, char y) { return CompareBlock(x, y); }
        }

        public static int CompareBlock(char x, char y) {
            return Block_CharList.IndexOf(x).CompareTo(Block_CharList.IndexOf(y));
        }
        private static List<char> Block_CharList = new List<char> {'Ａ','Ｂ','Ｃ','Ｄ','Ｅ','Ｆ','Ｇ','Ｈ','Ｉ','Ｊ','Ｋ','Ｌ','Ｍ','Ｎ','Ｏ','Ｐ','Ｑ','Ｒ','Ｓ','Ｔ','Ｕ','Ｖ','Ｗ','Ｘ','Ｙ','Ｚ',
                                                                           'ア','イ','ウ','エ','オ','カ','キ','ク','ケ','コ','サ','シ','ス','セ','ソ','タ','チ','ツ','テ','ト','ナ','ニ','ヌ','ネ','ノ','ハ','パ','ヒ','ピ','フ','プ','ヘ','ペ','ホ','ポ','マ','ミ','ム','メ','モ','ヤ','ユ','ヨ','ラ','リ','ル','レ','ロ',
                                                                           'あ','い','う','え','お','か','き','く','け','こ','さ','し','す','せ','そ','た','ち','つ','て','と','な','に','ぬ','ね','の','は','ひ','ふ','へ','ほ','ま','み','む','め','も','や','ゆ','よ','ら','り','る','れ'
        
                                                                  };
        // サークルのレイアウト用
        //-------------------------------------------------------------------------------
        #region +[static]GetCircleRect サークル位置四角形取得
        //-------------------------------------------------------------------------------
        //
        public static Rect GetCircleRect(int posx, int posy, int mapSizeW, int mapSizeH, int layout, int spaceNoSub, char bigarea) {
            int height, width;
            int offset_sub_x, offset_sub_y;
            switch (layout) {
                case 1: // (a,b) = (左,右)
                    height = mapSizeH;
                    width = mapSizeW / 2;
                    offset_sub_x = (spaceNoSub == 1) ? width : 0;
                    offset_sub_y = 0;
                    break;
                case 2: // (a,b) = (下,上)
                    height = mapSizeW / 2;
                    width = mapSizeH;
                    offset_sub_x = 0;
                    offset_sub_y = (spaceNoSub == 0) ? height : 0;
                    break;
                case 3: // (a,b) = (右,左)
                    height = mapSizeH;
                    width = mapSizeW / 2;
                    offset_sub_x = (spaceNoSub == 0) ? width : 0;
                    offset_sub_y = 0;
                    break;
                case 4: // (a,b) = (上,下)
                    height = mapSizeW / 2;
                    width = mapSizeH;
                    offset_sub_x = 0;
                    offset_sub_y = (spaceNoSub == 1) ? height : 0;
                    break;
                default:
                    return new Rect();
            }

            int xoffset = offset_sub_x;
            int yoffset = offset_sub_y;

            xoffset += 1;
            yoffset += 1;
            width -= 1;
            height -= 1;

            return new Rect(posx + xoffset, posy + yoffset, width, height);
        }
        #endregion (GetCircleRect)
    }
}
