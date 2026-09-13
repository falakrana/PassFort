using PasswordManager.Services.Encryption;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Interface for reading and writing encrypted vault file binary structures on disk.
/// Also covers hint and recovery key sidecar files.
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

    // ── Hint sidecar (hint.txt) ───────────────────────────────────────────────

    /// <summary>Reads the optional plain-text password hint. Returns null if no hint is set.</summary>
    string? ReadHint();

    /// <summary>Writes (or clears) the plain-text password hint sidecar file.</summary>
    void WriteHint(string? hint);

    // ── Recovery key sidecar (recovery.dat) ──────────────────────────────────

    /// <summary>Returns true if the recovery payload file exists on disk.</summary>
    bool RecoveryPayloadExists();

    /// <summary>Reads the encrypted recovery key payload from disk.</summary>
    EncryptedPayload ReadRecoveryPayload();

    /// <summary>Writes the encrypted recovery key payload to disk.</summary>
    void WriteRecoveryPayload(EncryptedPayload payload);

    /// <summary>Deletes the recovery payload file from disk.</summary>
    void DeleteRecoveryPayload();
}
