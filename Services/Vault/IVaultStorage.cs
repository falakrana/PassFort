using PasswordManager.Services.Encryption;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Interface for reading and writing encrypted vault file binary structures on disk.
/// </summary>
public interface IVaultStorage
{
    /// <summary>
    /// Checks whether the persistent vault file exists on disk.
    /// </summary>
    bool VaultExists();

    /// <summary>
    /// Reads and parses the encrypted binary vault file structure from disk.
    /// </summary>
    EncryptedPayload ReadVault();

    /// <summary>
    /// Writes the encrypted payload structure into binary vault file format on disk.
    /// </summary>
    void WriteVault(EncryptedPayload payload);

    /// <summary>
    /// Deletes the vault file from disk (e.g. for reset or testing).
    /// </summary>
    void DeleteVault();
}
