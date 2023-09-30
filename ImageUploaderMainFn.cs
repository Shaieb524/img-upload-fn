using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.Net.Http;
using System.Net;
using System.Collections;
using System.Security.Cryptography;
using System.IO.Compression;

namespace UCM.Functions
{
    public static class ImageUploaderMainFn
    {
        public static CloudBlockBlob ExtractBlobFile(string storageConnectionString, string imageName, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(imageName);

            return blockBlob;
        }

        public static async Task<MemoryStream> GetBlobFileStream(string storageConnectionString, string imageName, string containerName)
        {
            CloudBlockBlob blockBlob = ExtractBlobFile(storageConnectionString, imageName, containerName);

            if (blockBlob == null)
                return null;

            MemoryStream blobStream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(blobStream);
            blobStream.Position = 0;

            return blobStream;
        }

        public static async Task<CloudBlobContainer> PrepareNewBlobContainer(string storageConnectionString, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            if (!await container.ExistsAsync())
            {
                await container.CreateAsync();
                await container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            return container;
        }

        public static async Task<IActionResult> CreateBlobFile(string storageConnectionString, string containerName, string imageName, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                CloudBlobContainer container = await PrepareNewBlobContainer(storageConnectionString, containerName);
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(imageName ?? file.FileName);

                using (Stream stream = file.OpenReadStream())
                {
                    await blockBlob.UploadFromStreamAsync(stream);
                }

                return new OkObjectResult("Photo uploaded successfully.");
            }
            else
            {
                return new BadRequestObjectResult("No photo uploaded.");
            }
        }

        [FunctionName("ImageUploaderMainFn")]
        public static async Task<IActionResult> Run( 
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "Image/{imageName?}")] HttpRequest req,
            string imageName,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");

            switch (req.Method)
            {
                case "GET":
                    var blobStream = await GetBlobFileStream(storageConnectionString, imageName, containerName);

                    var response = new FileContentResult(blobStream.ToArray(), "image/jpeg");
                    response.FileDownloadName = imageName;
                    return response;

                case "POST":
                    IFormFile file = req.Form.Files.GetFile("photo");
                    return await CreateBlobFile(storageConnectionString, containerName, imageName, file);

            }

            return null;

        }
    }
}
