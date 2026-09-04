using PasswordManager.Models;

namespace PasswordManager.Services.Vault;

/// <summary>
/// Temporary in-memory implementation of IPasswordService for Phase 3 CRUD development.
/// Persistent encrypted vault storage will replace this in Phase 5.
/// </summary>
public class InMemoryPasswordService : IPasswordService
{
    private readonly List<PasswordEntry> _entries = new();

    public InMemoryPasswordService()
    {
        SeedSampleData();
    }

    public IEnumerable<PasswordEntry> GetAll()
    {
        return _entries.Select(e => e.Clone()).ToList();
    }

    public PasswordEntry? GetById(Guid id)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        return entry?.Clone();
    }

    public void Add(PasswordEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        
        var newEntry = entry.Clone();
        if (newEntry.Id == Guid.Empty)
        {
            newEntry.Id = Guid.NewGuid();
        }
        newEntry.CreatedAt = DateTime.UtcNow;
        newEntry.LastModifiedAt = DateTime.UtcNow;

        _entries.Add(newEntry);
    }

    public void Update(PasswordEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            var updated = entry.Clone();
            updated.LastModifiedAt = DateTime.UtcNow;
            _entries[index] = updated;
        }
    }

    public void Delete(Guid id)
    {
        _entries.RemoveAll(e => e.Id == id);
    }

    private void SeedSampleData()
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

        _entries.Add(new PasswordEntry
        {
            Id = Guid.NewGuid(),
            Title = "Personal Email",
            Username = "my.email@gmail.com",
            Password = "PersonalEmailPassword99",
            WebsiteUrl = "https://mail.google.com",
            Category = "Personal",
            Notes = "Main personal email address.",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
    }
}
