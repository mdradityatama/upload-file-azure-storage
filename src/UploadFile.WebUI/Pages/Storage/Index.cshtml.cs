using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UploadFile.WebUI.Model;

namespace UploadFile.WebUI.Pages.Storage
{
    public class IndexModel : PageModel
    {
        public string AccountKey { get; set; } = "5lnkBHbFAmKF2CXp1w2osGQiA+l1xOOosIUBHkjZyE6HXECDBsr6++EoTQ0ZBfM9Kk18LI+7/RYXpEBVPmwvpg==";
        public string AccountName { get; set; } = "stupfielddev";

        public Uri Urii { get; set; } = new Uri("https://stupfielddev.blob.core.windows.net");
        public Uri UriFile { get; set; } = new Uri("C:\\");

        public DataLakeFileSystemClient FileSystemClient { get; set; }
        public FileFolders FileFolders { get; set; }

        public string Message { get; set; }
        [BindProperty]
        public IFormFile Upload { get; set; }

        public async Task OnGet()
        {
            var dataLakeServiceClient = new DataLakeServiceClient(Urii);
            var ress = await GetDataLakeServiceClient(dataLakeServiceClient, AccountName, AccountKey);
            this.FileSystemClient = ress;

            var resFile = await ListFilesInDirectory(this.FileSystemClient);
            this.FileFolders = resFile;
        }

        public async Task OnPostDownload(string nameFile)
        {
            var dataLakeServiceClient = new DataLakeServiceClient(Urii);
            var ress = await GetDataLakeServiceClient(dataLakeServiceClient, AccountName, AccountKey);
            this.FileSystemClient = ress;

            var resFile = await ListFilesInDirectory(this.FileSystemClient);
            this.FileFolders = resFile;

            var Text = nameFile.Split("/");
            await DownloadFile(this.FileSystemClient, Text[1]);
        }

        public async Task OnPostUpload()
        {
            var dataLakeServiceClient = new DataLakeServiceClient(Urii);
            var ress = await GetDataLakeServiceClient(dataLakeServiceClient, AccountName, AccountKey);
            this.FileSystemClient = ress;

            this.Message = Upload.FileName;
            var file = Path.Combine(Directory.GetCurrentDirectory(), "uploads", Upload.FileName);
            var fileStream = new FileStream(file, FileMode.Create);
            await Upload.CopyToAsync(fileStream);
            await UploadFile(ress, fileStream, Upload.FileName);

            var resFile = await ListFilesInDirectory(this.FileSystemClient);
            this.FileFolders = resFile;
        }

        public async Task OnPostDelete(string nameFile)
        {
            var dataLakeServiceClient = new DataLakeServiceClient(Urii);
            var ress = await GetDataLakeServiceClient(dataLakeServiceClient, AccountName, AccountKey);
            this.FileSystemClient = ress;

            var Text = nameFile.Split("/");
            await DeleteFile(ress, Text[1]);

            var resFile = await ListFilesInDirectory(this.FileSystemClient);
            this.FileFolders = resFile;

        }

        public async Task<DataLakeFileSystemClient> GetDataLakeServiceClient(DataLakeServiceClient dataLakeServiceClient, string accountName, string accountKey)
        {
            StorageSharedKeyCredential sharedKeyCredential =
                new StorageSharedKeyCredential(accountName, accountKey);

            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";

            dataLakeServiceClient = new DataLakeServiceClient
                (new Uri(dfsUri), sharedKeyCredential);

            return dataLakeServiceClient.GetFileSystemClient("radit");
        }

        public async Task UploadFile(DataLakeFileSystemClient fileSystemClient, FileStream fileStream, string nameFile)
        {
            DataLakeDirectoryClient directoryClient =
                fileSystemClient.GetDirectoryClient("folder-1");

            DataLakeFileClient fileClient = await directoryClient.CreateFileAsync(nameFile);

            long fileSize = fileStream.Length;

            await fileClient.AppendAsync(fileStream, offset: 0);

            await fileClient.FlushAsync(position: fileSize);
        }

        public async Task<FileFolders> ListFilesInDirectory(DataLakeFileSystemClient fileSystemClient)
        {
            var fileFolders = new FileFolders();

            IAsyncEnumerator<PathItem> enumerator = fileSystemClient.GetPathsAsync("folder-1").GetAsyncEnumerator();

            await enumerator.MoveNextAsync();

            PathItem item = enumerator.Current;

            while (item != null)
            {
                fileFolders.NameFiles.Add(new NameFile
                {
                    Name = item.Name.ToString()
                });

                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                item = enumerator.Current;
            }

            return fileFolders;
        }

        public async Task DownloadFile(DataLakeFileSystemClient fileSystemClient, string nameFile)
        {
            DataLakeDirectoryClient directoryClient =
                fileSystemClient.GetDirectoryClient("folder-1");

            DataLakeFileClient fileClient =
                directoryClient.GetFileClient(nameFile);

            Response<FileDownloadInfo> downloadResponse = await fileClient.ReadAsync();

            BinaryReader reader = new BinaryReader(downloadResponse.Value.Content);

            FileStream fileStream =
                System.IO.File.OpenWrite(Path.Combine(Directory.GetCurrentDirectory(), "downloads", nameFile));

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

        public async Task DeleteFile(DataLakeFileSystemClient fileSystemClient, string nameFile)
        {
            DataLakeDirectoryClient directoryClient =
                fileSystemClient.GetDirectoryClient("folder-1");

            DataLakeFileClient fileClient =
                directoryClient.GetFileClient(nameFile);

            await fileClient.DeleteAsync();
        }
    }
}