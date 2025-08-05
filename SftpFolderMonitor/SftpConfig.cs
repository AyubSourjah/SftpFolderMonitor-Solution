using System.ComponentModel.DataAnnotations;

namespace SftpFolderMonitor;

public class SftpConfig
{
    [Required]
    public string Host { get; init; } = string.Empty;
    
    [Range(1, 65535)]
    public int Port { get; init; } = 22;
    
    [Required]
    public string Username { get; init; } = string.Empty;
    
    public string? Password { get; init; }
    
    public string? SshKeyPath { get; init; }
    
    [Required]
    public string AuthenticationMethod { get; init; } = "password";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("SFTP Host is required");

        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("SFTP Username is required");

        switch (AuthenticationMethod.ToLowerInvariant())
        {
            case "password":
                if (string.IsNullOrWhiteSpace(Password))
                    throw new InvalidOperationException("Password is required for password authentication");
                break;
            case "privatekey":
                if (string.IsNullOrWhiteSpace(SshKeyPath))
                    throw new InvalidOperationException("SSH key path is required for private key authentication");
                if (!File.Exists(SshKeyPath))
                    throw new InvalidOperationException($"SSH key file not found: {SshKeyPath}");
                break;
            default:
                throw new InvalidOperationException($"Unsupported authentication method: {AuthenticationMethod}");
        }
    }
}