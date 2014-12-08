using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.Storage;

namespace ComicStarViewer
{
    public static class Util
    {
        //-------------------------------------------------------------------------------
        #region +[async]RestoreFromXmlFile Xmlファイルからデータを復元
        //-------------------------------------------------------------------------------
        //
        public static async Task<Tuple<bool, T>> RestoreFromXmlFile<T>(string fileName)
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var files = await localDir.GetFilesAsync();
            var file = files.FirstOrDefault(sf => sf.Name == fileName);
            if (file == null) {
                return Tuple.Create(false, default(T));
            }
            else {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                try {
                    using (Stream stream = await file.OpenStreamForReadAsync()) {
                        using (XmlReader reader = XmlReader.Create(stream)) {
                            if (serializer.CanDeserialize(reader)) {
                                stream.Seek(0, SeekOrigin.Begin);
                                return Tuple.Create(true, (T)serializer.Deserialize(stream));
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            return Tuple.Create(false, default(T));
        }
        #endregion (RestoreFromXmlFile)
        //-------------------------------------------------------------------------------
        #region +[async]SaveToXmlFile Xmlファイルにデータを保存
        //-------------------------------------------------------------------------------
        //
        public static async Task<bool> SaveToXmlFile<T>(T data, string fileName)
        {
            var localDir = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await localDir.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            try {
                using (Stream stream = await file.OpenStreamForWriteAsync())
                using (StreamWriter writer = new StreamWriter(stream)) {
                    serializer.Serialize(writer, data);
                }
            }
            catch (Exception) { return false; }
            return true;
        }
        #endregion (SaveToXmlFile)
    }
}
