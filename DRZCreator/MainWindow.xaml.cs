using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace DRZCreator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Regex keywordRegex = new Regex("[^a-z0-9-]"), decimalRegex = new Regex(@"^-?\d+$|^(-?\d+)(\.\d+)?$");
        private string path = "";

        public MainWindow()
        {
            InitializeComponent();
            RegistryKey software = Registry.CurrentUser.OpenSubKey("Software", true);
            if (software != null)
            {
                RegistryKey jimengame = software.CreateSubKey("JIMENGAME").CreateSubKey("DRZCreator");
                int? a = (int?)jimengame.GetValue("FirstOpen");
                if (a == null || a == 0)
                {
                    MessageBox.Show(this, "谱面文件夹是直接存放谱面文件的文件夹（文件夹名是Keyword的那个）\n本框只显示一次", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    jimengame.SetValue("FirstOpen", 1);
                }
            }
        }

        private void FolderSelector_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            FolderPath.Text = dialog.FileName;
            path = dialog.FileName.Replace("\\", "/") + "/";
            string keyword = new DirectoryInfo(path).Name;
            if (!keywordRegex.IsMatch(keyword))
            {
                KeywordField.Text = keyword;
            }

            if (File.Exists(path + "info.txt"))
            {
                string[] infos = TryAutoFill(path + "info.txt");
                if (infos != null)
                {
                    TitleField.Text = infos[0];
                    ArtistField.Text = infos[1];
                    BPMField.Text = infos[2];
                }
            }
        }

        private string[] TryAutoFill(string infoPath)
        {
            StreamReader streamReader = new StreamReader(infoPath, Encoding.UTF8);
            string[] infos = new string[3];
            for (int i = 0; i < 3; i++)
            {
                infos[i] = streamReader.ReadLine();
                if (infos[i] == null)
                {
                    return null;
                }
            }

            if (infos.Length < 3)
            {
                return null;
            }

            return infos.AsQueryable().Take(3).ToArray();
        }

        private void ConfirmButon_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(path))
            {
                MessageBox.Show(this, "不选择要压缩的文件夹，你想压缩空气吗", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (keywordRegex.IsMatch(KeywordField.Text))
            {
                MessageBox.Show(this, "Keyword不合法", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] bpms = BPMField.Text.Split('~');
            if (bpms.Length != 1 && bpms.Length != 2)
            {
                MessageBox.Show(this, "BPM不合法", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (string bpm in bpms)
            {
                if (!decimalRegex.IsMatch(bpm))
                {
                    MessageBox.Show(this, "BPM不合法", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            DRZInfo drzInfo = new DRZInfo();
            drzInfo.keyword = KeywordField.Text;
            drzInfo.title = ToBase64(TitleField.Text);
            drzInfo.artist = ToBase64(ArtistField.Text);
            drzInfo.bpm = BPMField.Text;
            try
            {
                FileStream fileStream = new FileStream(path + "drzinfo", FileMode.Create, FileAccess.Write);
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(drzInfo, Formatting.None));
                fileStream.Write(bytes, 0, bytes.Length);
                if (ZipDirectory(path, "./" + drzInfo.keyword + ".tmp"))
                {
                    File.Move("./" + drzInfo.keyword + ".tmp", path + drzInfo.keyword + ".drz");
                    MessageBox.Show(this, "完成！\n文件在歌曲文件夹内", "信息", MessageBoxButton.OK, MessageBoxImage.Information,
                        MessageBoxResult.None, MessageBoxOptions.None);
                }
                else
                {
                    MessageBox.Show(this, "无法压缩文件夹，遇到未知错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error,
                        MessageBoxResult.None, MessageBoxOptions.None);
                }

                File.Delete(path + "drzinfo");
            }
            catch (Exception)
            {
                MessageBox.Show(this, "请删除歌曲文件夹内的drzinfo", "错误", MessageBoxButton.OK, MessageBoxImage.Error,
                    MessageBoxResult.None, MessageBoxOptions.None);
            }
        }

        private string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// ZIP：压缩文件夹
        /// add yuangang by 2016-06-13
        /// </summary>
        /// <param name="DirectoryToZip">需要压缩的文件夹（绝对路径）</param>
        /// <param name="ZipedPath">压缩后的文件路径（绝对路径）</param>
        /// <param name="ZipedFileName">压缩后的文件名称（文件名，默认 同源文件夹同名）</param>
        /// <param name="IsEncrypt">是否加密（默认 加密）</param>
        public static bool ZipDirectory(string DirectoryToZip, string ZipedPath)
        {
            //如果目录不存在，则报错
            if (!Directory.Exists(DirectoryToZip))
            {
                throw new FileNotFoundException("指定的目录: " + DirectoryToZip + " 不存在!");
            }

            try
            {
                using (FileStream ZipFile = File.Create(ZipedPath))
                {
                    using (ZipOutputStream s = new ZipOutputStream(ZipFile))
                    {
                        //压缩文件加密
                        s.Password = "44G644G944KI";

                        ZipSetp(DirectoryToZip, s, "");
                    }
                }

                return true;
            }
            catch (IOException)
            {
                File.Delete(ZipedPath);
                return false;
            }
        }

        /// <summary>
        /// 递归遍历目录
        /// add yuangang by 2016-06-13
        /// </summary>
        private static void ZipSetp(string strDirectory, ZipOutputStream s, string parentPath)
        {
            strDirectory = strDirectory.Replace("\\", "/");
            parentPath = parentPath.Replace("\\", "/");
            if (strDirectory[strDirectory.Length - 1] != '/')
            {
                strDirectory += '/';
            }

            Crc32 crc = new Crc32();

            string[] filenames = Directory.GetFileSystemEntries(strDirectory);

            for (int i = 0; i < filenames.Length; i++) // 遍历所有的文件和目录
            {
                string file = filenames[i];
                file = file.Replace("\\", "/");
                if (Directory.Exists(file)) // 先当作目录处理如果存在这个目录就递归Copy该目录下面的文件
                {
                    string pPath = parentPath;
                    pPath += file.Substring(file.LastIndexOf("/") + 1);
                    pPath += "/";
                    ZipSetp(file, s, pPath);
                }

                else // 否则直接压缩文件
                {
                    //打开压缩文件
                    using (FileStream fs = File.OpenRead(file))
                    {
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);

                        string fileName = parentPath + file.Substring(file.LastIndexOf("/") + 1);
                        ZipEntry entry = new ZipEntry(fileName);

                        entry.DateTime = DateTime.Now;
                        entry.Size = fs.Length;

                        fs.Close();

                        crc.Reset();
                        crc.Update(buffer);

                        entry.Crc = crc.Value;
                        s.PutNextEntry(entry);

                        s.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }
    }

    public struct DRZInfo
    {
        [JsonProperty("keyword")] public string keyword;
        [JsonProperty("title")] public string title;
        [JsonProperty("artist")] public string artist;
        [JsonProperty("bpm")] public string bpm;
    }
}