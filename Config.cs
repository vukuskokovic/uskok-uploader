namespace uskokuploader;

public class Config
{
    public string HostName { get; set; } = null!;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PrivateKey { get; set; }
    public int Port { get; set; }

    public UploadInfo[] Uploads { get; set; } = null!;
}

public class UploadInfo
{
    public string LocalPath { get; set; } = null!;
    public string[] RemotePath { get; set; } = null!;
}
