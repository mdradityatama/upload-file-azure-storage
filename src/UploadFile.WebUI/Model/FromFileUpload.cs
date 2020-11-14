using Microsoft.AspNetCore.Http;

namespace UploadFile.WebUI.Model
{
    public class FromFileUpload
    {
        public string Category { get; set; }
        public string DistributorCode { get; set; }
        public IFormFile FileUpload { get; set; }
    }
}