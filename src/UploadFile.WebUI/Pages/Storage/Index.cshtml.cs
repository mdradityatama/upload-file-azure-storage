using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UploadFile.WebUI.Model;

namespace UploadFile.WebUI.Pages.Storage
{
    public class IndexModel : PageModel
    {
        public const string AccountKey = "sDnbqLhlZwiEYy56QpxxatfrtX3glPCPWPnd3Pf3crFGwf9kV+DTqr4iuFvhvELHnVjt9TzbVcObP6BxPf532Q==";
        public const string AccountName = "stupfielddevrennie";
        public const string ContainerName = "reports";
        public const string FolderName = "2020-11-13";

        public DateTime DateTime { get; set; } = new DateTime();
        public Uri UriFile { get; set; } = new Uri("C:\\");

        public AzureFolder AzureFolder { get; set; }

        private readonly IWebHostEnvironment _webHostEnvironment;

        public IndexModel(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public FromFileUpload FormFileUpload { get; set; }

        public async Task OnGet()
        {
            this.AzureFolder = await GetAzureFolder();
        }

        public async Task OnPostDownload(string nameFile)
        {
            this.AzureFolder = await GetAzureFolder();

            var Text = nameFile.Split("/");
            await DownloadFile(Text[2]);
        }

        public async Task OnPostUpload()
        {
            string filename = System.Net.Http.Headers.ContentDispositionHeaderValue.Parse(FormFileUpload.FileUpload.ContentDisposition).FileName.Trim('"');

            filename = EnsureCorrectFilename(filename);

            string fullPath = this.GetPathAndFilenameForUpload(filename);

            using (var output = System.IO.File.Create(fullPath))
            {
                await FormFileUpload.FileUpload.CopyToAsync(output);
            }

            await UploadFile(fullPath);

            this.AzureFolder = await GetAzureFolder();
        }

        public async Task OnPostDelete(string nameFile)
        {
            var fileName = nameFile.Split("/")[1];
            await DeleteFile(fileName);

            this.AzureFolder = await GetAzureFolder();
        }

        public async Task UploadFile(string fullPath)
        {
            var dataLakeServiceClient = GetDataLakeServiceClient();
            var dataLakeFileSystemClient = dataLakeServiceClient.GetFileSystemClient(ContainerName);

            var directoryDate = dataLakeFileSystemClient.GetDirectoryClient(DateTime.Now.ToString("yyyy-MM-dd"));

            #region Create and Check Folder
            DataLakeDirectoryClient directoryDistributorCode = null;

            if (!directoryDate.Exists())
            {
                dataLakeFileSystemClient.CreateDirectory(DateTime.Now.ToString("yyyy-MM-dd"));
                directoryDate = dataLakeFileSystemClient.GetDirectoryClient(DateTime.Now.ToString("yyyy-MM-dd"));
            }

            if (FormFileUpload.Category.ToUpper() == "STOCK")
            {
                var directoryStock = directoryDate.GetSubDirectoryClient("Stock");

                if (!directoryStock.Exists())
                {
                    directoryDate.GetSubDirectoryClient("Stock");
                    directoryStock = directoryDate.GetSubDirectoryClient("Stock");
                }

                directoryDistributorCode = directoryStock.GetSubDirectoryClient(FormFileUpload.DistributorCode);

                if (!directoryDistributorCode.Exists())
                {
                    directoryStock.GetSubDirectoryClient(FormFileUpload.DistributorCode);
                    directoryDistributorCode = directoryStock.GetSubDirectoryClient(FormFileUpload.DistributorCode);
                }
            }

            if (FormFileUpload.Category.ToUpper() == "SALE")
            {
                var directorySales = directoryDate.GetSubDirectoryClient("Sales");

                if (!directorySales.Exists())
                {
                    directoryDate.GetSubDirectoryClient("Sales");
                    directorySales = directoryDate.GetSubDirectoryClient("Sales");
                }

                directoryDistributorCode = directorySales.GetSubDirectoryClient(FormFileUpload.DistributorCode);

                if (!directoryDistributorCode.Exists())
                {
                    directorySales.GetSubDirectoryClient(FormFileUpload.DistributorCode);
                    directoryDistributorCode = directorySales.GetSubDirectoryClient(FormFileUpload.DistributorCode);
                }
            }
            #endregion

            string fileName = Path.GetFileName(fullPath);

            DataLakeFileClient fileClient = await directoryDistributorCode.CreateFileAsync(fileName);

            using var fileStream = System.IO.File.OpenRead(fullPath);
            long fileSize = fileStream.Length;
            await fileClient.AppendAsync(fileStream, offset: 0);
            await fileClient.FlushAsync(position: fileSize);
        }

        public async Task<AzureFolder> GetAzureFolder()
        {
            var dataLakeServiceClient = GetDataLakeServiceClient();
            var dataLakeFileSystemClient = dataLakeServiceClient.GetFileSystemClient(ContainerName);

            var azureFolder = new AzureFolder { Name = FolderName };

            var sumFolder = new string[1] {"2020-11-13"};

            var directoryDate = dataLakeFileSystemClient.GetDirectoryClient(FolderName);
            var directorySales = directoryDate.GetSubDirectoryClient("Sales");
            Console.WriteLine("path sales: " + directorySales.Path);

            for (int i = 0; i < sumFolder.Length; i++)
            {
                IAsyncEnumerator<PathItem> enumerator = dataLakeFileSystemClient.GetPathsAsync(directorySales.Path).GetAsyncEnumerator();

                await enumerator.MoveNextAsync();

                //if (checkFile)
                //{
                //    await CreateDirectory(dataLakeFileSystemClient, pathToday);
                //}

                PathItem item = enumerator.Current;

                while (item != null)
                {
                    azureFolder.Files.Add(new AzureFile
                    {
                        Name = item.Name
                    });

                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    item = enumerator.Current;
                } 
            }

            return azureFolder;
        }
        public async Task CreateDirectory(DataLakeFileSystemClient fileSystemClient, string nameDirectory)
        {
            await fileSystemClient.CreateDirectoryAsync(nameDirectory + "/Distributor");
            await GetAzureFolder();
        }

        public async Task DownloadFile(string fileName)
        {
            var dataLakeServiceClient = GetDataLakeServiceClient();
            var dataLakeFileSystemClient = dataLakeServiceClient.GetFileSystemClient(ContainerName);
            var directoryClient = dataLakeFileSystemClient.GetDirectoryClient(FolderName);
            var fileClient = directoryClient.GetFileClient(fileName);

            Response<FileDownloadInfo> downloadResponse = await fileClient.ReadAsync();

            var reader = new BinaryReader(downloadResponse.Value.Content);

            string fullPath = GetPathAndFilenameForDownload(fileName);

            var fileStream = System.IO.File.OpenWrite(fullPath);

            int bufferSize = 4096;

            byte[] buffer = new byte[bufferSize];

            int count;

            while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
            {
                fileStream.Write(buffer, 0, count);
            }

            await fileStream.FlushAsync();

            fileStream.Close();
        }

        public async Task DeleteFile(string fileName)
        {
            var dataLakeServiceClient = GetDataLakeServiceClient();
            var dataLakeFileSystemClient = dataLakeServiceClient.GetFileSystemClient(ContainerName);
            var directoryClient = dataLakeFileSystemClient.GetDirectoryClient(FolderName);
            var fileClient = directoryClient.GetFileClient(fileName);

            await fileClient.DeleteAsync();
        }

        public static DataLakeServiceClient GetDataLakeServiceClient()
        {
            StorageSharedKeyCredential sharedKeyCredential = new StorageSharedKeyCredential(AccountName, AccountKey);

            string dfsUri = "https://" + AccountName + ".dfs.core.windows.net";

            return new DataLakeServiceClient(new Uri(dfsUri), sharedKeyCredential);
        }

        private string EnsureCorrectFilename(string filename)
        {
            if (filename.Contains("\\"))
                filename = filename.Substring(filename.LastIndexOf("\\") + 1);

            return filename;
        }

        private string GetPathAndFilenameForUpload(string filename)
        {
            return _webHostEnvironment.WebRootPath + "\\uploads\\" + filename;
        }

        private string GetPathAndFilenameForDownload(string filename)
        {
            return _webHostEnvironment.WebRootPath + "\\downloads\\" + filename;
        }
    }
}