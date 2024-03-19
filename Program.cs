using CommandLine;
using ConsoleTables;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using HeyRed.Mime;
using File = Google.Apis.Drive.v3.Data.File;

const string applicationName = "Google Drive Helper";

var username = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_USERNAME") ??
               throw new InvalidOperationException("Missing GOOGLE_DRIVE_USERNAME environment variable.");
var accessToken = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_ACCESS_TOKEN") ??
                  throw new InvalidOperationException("Missing GOOGLE_DRIVE_ACCESS_TOKEN environment variable.");
var refreshToken = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_REFRESH_TOKEN") ??
                   throw new InvalidOperationException("Missing GOOGLE_DRIVE_REFRESH_TOKEN environment variable.");
var clientId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID") ??
               throw new InvalidOperationException("Missing GOOGLE_DRIVE_CLIENT_ID environment variable.");
var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET") ??
                   throw new InvalidOperationException("Missing GOOGLE_DRIVE_CLIENT_SECRET environment variable.");

Parser.Default.ParseArguments<UploadOptions, ListOptions, ExportOptions>(args)
    .WithParsed(o =>
    {
        switch (o)
        {
            case UploadOptions uploadOptions:
            {
                using var file = System.IO.File.OpenRead(uploadOptions.File);

                if (string.IsNullOrEmpty(uploadOptions.Name))
                {
                    uploadOptions.Name = Path.GetFileName(uploadOptions.File);
                }

                if (string.IsNullOrEmpty(uploadOptions.Mime))
                {
                    uploadOptions.Mime = MimeTypesMap.GetMimeType(uploadOptions.File);

                    Console.WriteLine(
                        $"\u26a0 Mime type not provided. Using file extension to determine mime type.  [{uploadOptions.Mime}]");
                }

                var fileId = UploadFile(file,
                    uploadOptions.Name,
                    uploadOptions.Mime,
                    uploadOptions.Parent,
                    uploadOptions.Description);

                Console.WriteLine($"\u2705  {uploadOptions.File} successfully uploaded. [{fileId}]");

                break;
            }
            case ListOptions listOptions:
            {
                var files = ListFiles(listOptions.Folder, listOptions.PageSize);
                var table = new ConsoleTable("", "Name", "Id", "Size (bytes)", "Mime", "Description");

                foreach (var (file, index) in files.Select((value, i) => (value, i + 1)))
                {
                    table.AddRow(index, file.Name, file.Id, file.Size, file.MimeType, file.Description);
                }

                table.Write();

                break;
            }

            case ExportOptions exportOptions:
            {
                if (string.IsNullOrEmpty(exportOptions.Mime))
                {
                    exportOptions.Mime = MimeTypesMap.GetMimeType(exportOptions.Mime);

                    Console.WriteLine(
                        $"\u26a0 Mime type not provided. Using file extension to determine mime type.  [{exportOptions.Mime}]");
                }

                ExportFile(exportOptions.Id, exportOptions.Mime);

                break;
            }
        }
    });

DriveService GetService()
{
    var tokenResponse = new TokenResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken
    };

    var apiCodeFlow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
    {
        ClientSecrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        },
        Scopes = new[] { DriveService.Scope.Drive },
        DataStore = new FileDataStore(applicationName)
    });

    var credential = new UserCredential(apiCodeFlow, username, tokenResponse);
    var service = new DriveService(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = applicationName
    });

    return service;
}

string GetExportFilename(string name, string mime)
{
    var extension = MimeTypesMap.GetExtension(mime);
    return $"{name}.{extension}";
}

void ExportFile(string fileId, string mime)
{
    var service = GetService();
    var request = service.Files.Export(fileId, mime);
    var fileName = service.Files.Get(fileId).Execute().Name;

    Console.WriteLine($"Filename: {fileName}");

    var output = GetExportFilename(fileName, mime);

    var stream = new FileStream(output, FileMode.Create, FileAccess.Write);

    request.MediaDownloader.ProgressChanged += progress =>
    {
        Console.Write($"\r\u231b  Downloading... {progress.Status} {progress.BytesDownloaded} bytes");
    };

    request.Download(stream);

    Console.WriteLine($"\n\u2705  {output} successfully exported.");
}

File? GetExistingFile(string fileName, string folder)
{
    var service = GetService();

    var listRequest = service.Files.List();
    listRequest.Q = $"'{folder}' in parents and name = '{fileName}'";
    listRequest.Fields = "files(id)";
    listRequest.IncludeItemsFromAllDrives = true;
    listRequest.SupportsAllDrives = true;

    var files = listRequest.Execute().Files;
    return files.FirstOrDefault();
}

string UploadFile(Stream file, string fileName, string fileMime, string folder, string fileDescription)
{
    var service = GetService();

    // Check if the file already exists in the specified folder
    var existingFile = GetExistingFile(fileName, folder);

    var fileMetadata = new File
    {
        Name = fileName,
        Description = fileDescription,
        MimeType = fileMime
    };

    if (existingFile != null)
    {
        // Update the existing file
        var updateRequest = service.Files.Update(fileMetadata, existingFile.Id, file, fileMime);
        updateRequest.Fields = "id";
        updateRequest.SupportsAllDrives = true;
        var updateResponse = updateRequest.Upload();

        if (updateResponse.Status != UploadStatus.Completed)
        {
            throw updateResponse.Exception;
        }

        return existingFile.Id;
    }

    fileMetadata.Parents = new List<string> { folder };

    // Create a new file
    var createRequest = service.Files.Create(fileMetadata, file, fileMime);
    createRequest.Fields = "id";
    createRequest.SupportsAllDrives = true;
    var createResponse = createRequest.Upload();

    if (createResponse.Status != UploadStatus.Completed)
    {
        throw createResponse.Exception;
    }

    return createRequest.ResponseBody.Id;
}

IEnumerable<File> ListFiles(string folder, int pageSize)
{
    var service = GetService();

    var listRequest = service.Files.List();
    listRequest.PageSize = pageSize;
    listRequest.Q = $"'{folder}' in parents";
    listRequest.Fields = "nextPageToken, files(id, name, size, mimeType, description)";
    listRequest.IncludeItemsFromAllDrives = true;
    listRequest.SupportsAllDrives = true;

    var requestResult = listRequest.Execute();

    if (requestResult.Files != null && requestResult.Files.Count > 0)
    {
        return requestResult.Files;
    }

    return new List<File>();
}

[Verb("upload", HelpText = "Upload a file to Google Drive.")]
public class UploadOptions
{
    [Option('f', "file", Required = true, HelpText = "File to upload.")]
    public string File { get; set; }

    [Option('n', "name", Required = false, HelpText = "Name of the file.")]
    public string Name { get; set; }

    [Option('p', "parent", Required = true, HelpText = "Parent folder id.")]
    public string Parent { get; set; }

    [Option('m', "mime", Required = false, HelpText = "Mime type of the file.")]
    public string Mime { get; set; }

    [Option('d', "description", Default = "Uploaded with Google Drive Helper", Required = false,
        HelpText = "Description of the file.")]
    public string Description { get; set; }
}

[Verb("list", HelpText = "List files in a folder.")]
public class ListOptions
{
    [Option('f', "folder", Required = true, HelpText = "Folder id.")]
    public string Folder { get; set; }

    [Option('p', "pageSize", Default = 10, Required = false, HelpText = "Page size.")]
    public int PageSize { get; set; }
}

[Verb("export", HelpText = "Export a file from Google Drive.")]
public class ExportOptions
{
    [Option('i', "id", Required = true, HelpText = "File id.")]
    public string Id { get; set; }

    [Option('m', "mime", Required = false, HelpText = "Mime type of the file.")]
    public string Mime { get; set; }
}