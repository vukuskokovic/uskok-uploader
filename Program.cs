using WinSCP;
using System.Text.Json;
using uskokuploader;
using System.Collections.Concurrent;
using System.IO;

const string AppConfigPath = "appconfig.json";

if (!File.Exists(AppConfigPath))
{
    File.WriteAllText(AppConfigPath, JsonSerializer.Serialize(new Config()));
}
string AppConfigJson = await File.ReadAllTextAsync(AppConfigPath);
Config AppConfig = JsonSerializer.Deserialize<Config>(AppConfigJson)!;
if(AppConfig == null!)
{
    Console.WriteLine("Please fix appconfig.json remove if you want to reset to default...");
    Console.ReadKey();
    return;
}

if(AppConfig.Uploads is null or { Length: 0 })
{
    Console.WriteLine("No uploads provided...");
    Console.ReadKey();
    return;
}

Session WinscpSession = new();
SessionOptions LoginDetails = new()
{
    HostName = AppConfig.HostName,
    UserName = AppConfig.Username,
    Password = AppConfig.Password,
    PortNumber = AppConfig.Port,
    SshHostKeyPolicy = SshHostKeyPolicy.AcceptNew,
    SshPrivateKeyPath = AppConfig.PrivateKey
};

ConcurrentDictionary<string, DateTime> FileDatabase = new();
bool Connect()
{
    try
    {
        WinscpSession?.Dispose();

        WinscpSession = new Session();
        WinscpSession.Open(LoginDetails);
        Console.WriteLine("Connected to the server");
        return true;
    }
    catch(Exception ex)
    {
        Console.WriteLine("Error connecting " + ex.Message);
        return false;
    }
}
while (!Connect())
    await Task.Delay(1000);


async Task Upload(string file, UploadInfo owner)
{
    FileStream? fs = null;
    try
    {
        fs = new(file, FileMode.Open);
    }
    catch(Exception ex)
    {
        Console.WriteLine($"Error openning file {file}: {ex.Message}");
        return;
    }
    var pathParts = Path.GetRelativePath(owner.LocalPath, file).Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    var finalPath = string.Empty;

    foreach(var pathPart in pathParts)
    {
        finalPath = RemotePath.Combine(finalPath, pathPart);
    }
    int uploadIndex = 0;
    while (uploadIndex < owner.RemotePath.Length)
    {
        try
        {
            fs.Position = 0;
            var pathToUpload = RemotePath.Combine(owner.RemotePath[uploadIndex], finalPath);
            Console.WriteLine("Uploading to {0}", pathToUpload);
            WinscpSession.PutFile(fs, pathToUpload);
            uploadIndex++;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error uploading file {0}, {1}", file, ex.Message);
            while (!Connect())
                await Task.Delay(1000);
        }
    }
    Console.WriteLine("Uploaded {0}", file);
    fs.Dispose();
}

Task FileTask(string path, UploadInfo owner, bool onrun)
{
    DateTime LastWrite = File.GetLastWriteTime(path);
    DateTime value = FileDatabase.GetOrAdd(path, LastWrite);

    if (onrun || LastWrite == value) return Task.CompletedTask;

    FileDatabase[path] = LastWrite;
    return Upload(path, owner);
}

Task SearchDirectory(string path, UploadInfo owner, bool onrun = false)
{
    if (!Directory.Exists(path)) return Task.CompletedTask;
    var fileTasks = Task.WhenAll(Directory.GetFiles(path).Select(file => FileTask(file, owner, onrun)));
    var folderTasks = Task.WhenAll(Directory.GetDirectories(path).Select(directory => SearchDirectory(directory, owner, onrun)));

    return Task.WhenAll(fileTasks, folderTasks);
}
Console.WriteLine("Getting all files");
await Task.WhenAll(AppConfig.Uploads.Select(uploadInfo => SearchDirectory(uploadInfo.LocalPath, uploadInfo, true)));


Console.WriteLine("Scanned " + FileDatabase.Count + " files");
while (true)
{
    await Task.WhenAll(AppConfig.Uploads.Select(uploadInfo => SearchDirectory(uploadInfo.LocalPath, uploadInfo, false)));
    await Task.Delay(1000);
}