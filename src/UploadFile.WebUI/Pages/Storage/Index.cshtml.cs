using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UploadFile.WebUI.Model;

namespace UploadFile.WebUI.Pages.Storage
{
    public class IndexModel : PageModel
    {
        public const string AccountKey = "5lnkBHbFAmKF2CXp1w2osGQiA+l1xOOosIUBHkjZyE6HXECDBsr6++EoTQ0ZBfM9Kk18LI+7/RYXpEBVPmwvpg==";
        public const string AccountName = "stupfielddev";
        public const string ContainerName = "radit";
        public const string FolderName = "folder-1";

        public Uri UriFile { get; set; } = new Uri("C:\\");

        public AzureFolder AzureFolder { get; set; }

        private readonly IWebHostEnvironment _webHostEnvironment;

        public IndexModel(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public IFormFile FormFileUpload { get; set; }

        public async Task OnGet()
        {
            this.AzureFolder = await GetAzureFolder();
        }

        public async Task OnPostDownload(string nameFile)
        {
            this.AzureFolder = await GetAzureFolder();

            var Text = nameFile.Split("/");
            await DownloadFile(Text[1]);
        }

        public async Task OnPostUpload()
        {
            string filename = System.Net.Http.Headers.ContentDispositionHeaderValue.Parse(FormFileUpload.ContentDisposition).FileName.Trim('"');

            filename = EnsureCorrectFilename(filename);

            string fullPath = this.GetPathAndFilenameForUpload(filename);

            using (var output = System.IO.File.Create(fullPath))
            {
                await FormFileUpload.CopyToAsync(output);
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
            var directoryClient = dataLakeFileSystemClient.GetDirectoryClient(FolderName);

            string fileName = Path.GetFileName(fullPath);

            DataLakeFileClient fileClient = await directoryClient.CreateFileAsync(fileName);

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

            IAsyncEnumerator<PathItem> enumerator = dataLakeFileSystemClient.GetPathsAsync(FolderName).GetAsyncEnumerator();

            await enumerator.MoveNextAsync();

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

            return azureFolder;
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