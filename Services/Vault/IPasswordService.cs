using PasswordManager.Models;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Service interface defining password entry repository and vault CRUD operations.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Retrieves all password entries stored in the vault.
    /// </summary>
    IEnumerable<PasswordEntry> GetAll();

    /// <summary>
    /// Retrieves a specific password entry by its unique identifier.
    /// </summary>
    PasswordEntry? GetById(Guid id);

    /// <summary>
    /// Adds a new password entry to the vault.
    /// </summary>
    void Add(PasswordEntry entry);

    /// <summary>
    /// Updates an existing password entry in the vault.
    /// </summary>
    void Update(PasswordEntry entry);

    /// <summary>
    /// Deletes a password entry from the vault by its unique identifier.
    /// </summary>
    void Delete(Guid id);
}
