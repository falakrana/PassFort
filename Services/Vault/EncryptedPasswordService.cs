using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PasswordManager.Models;
using PasswordManager.Services.Authentication;
using PasswordManager.Services.Encryption;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Persistent encrypted implementation of IPasswordService.
/// Encrypts vault contents at rest with AES-256-GCM using keys derived from the master password.
/// Wipes plaintext entries from memory when locked.
/// </summary>
public class EncryptedPasswordService : IPasswordService
{
    private readonly IAuthenticationService _authService;
    private readonly IEncryptionService _encryptionService;
    private readonly IVaultStorage _vaultStorage;
    private readonly List<PasswordEntry> _entries = new();

    public EncryptedPasswordService(
        IAuthenticationService authService,
        IEncryptionService encryptionService,
        IVaultStorage vaultStorage)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _vaultStorage = vaultStorage ?? throw new ArgumentNullException(nameof(vaultStorage));

        _authService.LockStateChanged += OnLockStateChanged;

        // If vault is already unlocked at construction, load initial state
        if (_authService.IsUnlocked)
        {
            LoadVault();
        }
    }

    public IEnumerable<PasswordEntry> GetAll()
    {
        EnsureUnlocked();
        return _entries.Select(e => e.Clone()).ToList();
    }

    public PasswordEntry? GetById(Guid id)
    {
        EnsureUnlocked();
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        return entry?.Clone();
    }

    public void Add(PasswordEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        EnsureUnlocked();

        var newEntry = entry.Clone();
        if (newEntry.Id == Guid.Empty)
        {
            newEntry.Id = Guid.NewGuid();
        }
        newEntry.CreatedAt = DateTime.UtcNow;
        newEntry.LastModifiedAt = DateTime.UtcNow;

        _entries.Add(newEntry);
        SaveVault();
    }

    public void Update(PasswordEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        EnsureUnlocked();

        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            var updated = entry.Clone();
            updated.LastModifiedAt = DateTime.UtcNow;
            _entries[index] = updated;
            SaveVault();
        }
    }

    public void Delete(Guid id)
    {
        EnsureUnlocked();
        int removedCount = _entries.RemoveAll(e => e.Id == id);
        if (removedCount > 0)
        {
            SaveVault();
        }
    }

    private void OnLockStateChanged()
    {
        if (_authService.IsUnlocked)
        {
            LoadVault();
        }
        else
        {
            // Wipe sensitive plaintext entries from memory upon locking
            _entries.Clear();
        }
    }

    private void LoadVault()
    {
        _entries.Clear();

        if (!_vaultStorage.VaultExists())
        {
            // First-run initialization: create initial sample entries and save encrypted vault
            SeedInitialSampleEntries();
            SaveVault();
            return;
        }

        byte[] key = _authService.ActiveKey
            ?? throw new InvalidOperationException("Cannot load vault: Authentication key is missing.");

        var payload = _vaultStorage.ReadVault();
        byte[] plaintextBytes = _encryptionService.Decrypt(payload, key);
        try
        {
            var loadedEntries = JsonSerializer.Deserialize<List<PasswordEntry>>(plaintextBytes);
            if (loadedEntries != null)
            {
                _entries.AddRange(loadedEntries);
            }
        }
        finally
        {
            // Sensitive memory hygiene: zero out plaintext byte array
            Array.Clear(plaintextBytes, 0, plaintextBytes.Length);
        }
    }

    private void SaveVault()
    {
        EnsureUnlocked();

        byte[] key = _authService.ActiveKey
            ?? throw new InvalidOperationException("Cannot save vault: Authentication key is missing.");
        byte[] salt = _authService.ActiveSalt
            ?? throw new InvalidOperationException("Cannot save vault: Salt is missing.");

        byte[] plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(_entries);
        try
        {
            var payload = _encryptionService.Encrypt(plaintextBytes, key, salt);
            _vaultStorage.WriteVault(payload);
        }
        finally
        {
            // Sensitive memory hygiene: zero out plaintext byte array
            Array.Clear(plaintextBytes, 0, plaintextBytes.Length);
        }
    }

    private void EnsureUnlocked()
    {
        if (!_authService.IsUnlocked || _authService.ActiveKey == null)
        {
            throw new InvalidOperationException("Vault is locked. Authenticate with master password first.");
        }
    }

    private void SeedInitialSampleEntries()
    {
        _entries.Add(new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = "GitHub Account",
            Username = "dev_user@example.com",
            Password = "SuperSecretGitHubPassword123!",
            WebsiteUrl = "https://github.com",
            Category = "Development",
            Notes = "Primary personal GitHub account with 2FA enabled.",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });

        _entries.Add(new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = "Work Google Suite",
            Username = "user@company.com",
            Password = "CompanyWorkPass#2026",
            WebsiteUrl = "https://workspace.google.com",
            Category = "Work",
            Notes = "Corporate email and drive access.",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
    }
}
