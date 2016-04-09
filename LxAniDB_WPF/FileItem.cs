using System.IO;

namespace LxAniDB_WPF
{
    public class FileItem
    {
        private string filePath;

        public string FilePath
        {
            get { return filePath; }
            set
            {
                filePath = value;
                this.FileName = Path.GetFileName(value);
            }
        }

        public string FileName { get; private set; }
    }
}