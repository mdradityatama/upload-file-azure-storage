using System.Collections.Generic;

namespace UploadFile.WebUI.Model
{
    public class AzureFolder
    {
        public string Name { get; set; }
        public IList<AzureFile> Files { get; set; } = new List<AzureFile>();
    }

    public class AzureFile
    {
        public string Name { get; set; }
    }
}