# Google Drive Helper

Google Drive Helper is a C# console application that allows you to interact with Google Drive using the Google Drive
API. You can upload files to Google Drive and list files in a specific folder.

## Prerequisites

Before running the application, make sure you have the following:

- .NET SDK installed on your machine
- Google Drive API credentials (client ID, client secret, access token, refresh token)

## Installation

1. Clone the repository or download the source code.
2. Open the project in your preferred IDE or text editor.
3. Restore the NuGet packages by running the following command in the project directory:

```bash
dotnet restore
```

4. Set the following environment variables with your Google Drive API credentials:
    - `GOOGLE_DRIVE_USERNAME`: Your Google Drive username
    - `GOOGLE_DRIVE_ACCESS_TOKEN`: Access token for authentication
    - `GOOGLE_DRIVE_REFRESH_TOKEN`: Refresh token for authentication
    - `GOOGLE_DRIVE_CLIENT_ID`: Client ID of your Google Drive API project
    - `GOOGLE_DRIVE_CLIENT_SECRET`: Client secret of your Google Drive API project

## Usage

The application supports two main operations: uploading a file and listing files in a folder.

### Uploading a File

To upload a file to Google Drive, use the `upload` command followed by the required options:

```
dotnet run -- upload -f <file_path> -p <parent_folder_id> [-n <file_name>] [-m <mime_type>] [-d <description>]
```

Options:

- `-f`, `--file`: Path to the file you want to upload (required)
- `-p`, `--parent`: ID of the parent folder where the file will be uploaded (required)
- `-n`, `--name`: Name of the file (optional, defaults to the original file name)
- `-m`, `--mime`: MIME type of the file (optional, detected automatically if not provided)
- `-d`, `--description`: Description of the file (optional, defaults to "Uploaded with Google Drive Helper")

Example:

```
dotnet run -- upload -f "/path/to/file.txt" -p "1234567890abcdefg" -n "MyFile.txt" -d "Important file"
```

### Listing Files in a Folder

To list files in a specific folder on Google Drive, use the `list` command followed by the required options:

```
dotnet run -- list -f <folder_id> [-p <page_size>]
```

Options:

- `-f`, `--folder`: ID of the folder you want to list files from (required)
- `-p`, `--pageSize`: Number of files to retrieve per page (optional, defaults to 10)

Example:

```
dotnet run -- list -f "1234567890abcdefg" -p 20
```

## Dependencies

The application uses the following NuGet packages:

- `CommandLineParser`: For parsing command-line arguments
- `ConsoleTables`: For displaying the list of files in a tabular format
- `Google.Apis.Drive.v3`: For interacting with the Google Drive API
- `HeyRed.Mime`: For detecting MIME types based on file extensions

## License

This project is licensed under the [MIT License](LICENSE).