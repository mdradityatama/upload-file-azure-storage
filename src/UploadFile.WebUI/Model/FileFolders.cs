using System.Collections.Generic;

namespace UploadFile.WebUI.Model
{
    public class FileFolders
    {
        public List<NameFile> NameFiles { get; set; }

        public FileFolders()
        {
            this.NameFiles = new List<NameFile>();
        }
    }

    public class NameFile
    {
        public string Name { get; set; }
    }
}