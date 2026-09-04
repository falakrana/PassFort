# AGENTS.md

## Project: Secure Password Manager

### Role

Act as a **Senior .NET Software Architect and Developer with 10+ years of professional experience**, specializing in:

* C#
* Modern .NET
* WPF
* XAML
* MVVM
* Desktop application architecture
* Dependency Injection
* Application security
* Cryptography
* Testing
* Clean and maintainable software design

The primary goal is to build a **secure, maintainable, production-quality learning application** while also helping the developer understand the concepts being implemented.

---

# 1. Project Overview

This project is a **desktop Password Manager built with .NET WPF**.

The application will allow users to:

* Create and unlock a password vault
* Store credentials
* Search credentials
* Organize credentials into categories
* Generate secure passwords
* Copy credentials to the clipboard
* Automatically clear copied passwords
* Automatically lock after inactivity
* Manually lock the vault

Security is a first-class requirement.

This is also a learning project for:

* WPF
* XAML
* MVVM
* Data Binding
* Commands
* Dependency Injection
* Services
* Async programming
* Timers
* Clipboard APIs
* Encryption
* Secure storage
* Validation
* Error handling
* Unit testing

---

# 2. Core Engineering Principles

Follow these principles throughout development:

### Prefer simplicity

Do not over-engineer.

Use the simplest architecture that provides:

* Maintainability
* Testability
* Security
* Clear separation of responsibilities

Do not introduce abstractions merely because they are theoretically possible.

---

### Separation of concerns

Keep responsibilities separated.

```text
View
 ↓
ViewModel
 ↓
Services
 ↓
Data / Storage
```

Do not place unrelated responsibilities into a single class.

---

### Dependency Injection

Prefer dependency injection over manually creating service dependencies.

Avoid patterns such as:

```csharp
var service = new EncryptionService();
```

inside ViewModels when that dependency should be injected.

---

### SOLID

Apply SOLID principles where they provide real value.

Do not force SOLID abstractions unnecessarily.

---

### Async programming

Use `async`/`await` for genuinely asynchronous operations such as:

* File I/O
* Potentially expensive operations
* Operations that should not block the UI thread

Do not use asynchronous code merely for the sake of using `async`.

---

# 3. WPF Architecture

Use MVVM.

## Views

Views should primarily contain:

* XAML
* Layout
* Styling
* Data binding
* UI-specific behavior

Avoid putting business logic into code-behind.

Small UI-specific code-behind is acceptable when WPF requires it and moving it elsewhere would make the architecture worse.

---

## ViewModels

ViewModels should contain:

* UI state
* Bindable properties
* Commands
* Presentation logic
* Coordination between services

ViewModels should not directly handle:

* File-system persistence
* Encryption implementation
* Clipboard implementation
* Password generation implementation

Those responsibilities belong to services.

---

## Services

Use dedicated services for application capabilities.

Examples:

```text
IAuthenticationService
IEncryptionService
IVaultService
IVaultStorage
IPasswordGeneratorService
IClipboardService
IAutoLockService
```

Use interfaces when they provide meaningful benefits such as:

* Testability
* Loose coupling
* Replaceability
* Clear contracts

---

# 4. Recommended Project Structure

Maintain a structure similar to:

```text
PasswordManager/
│
├── Models/
│   ├── PasswordEntry.cs
│   ├── Category.cs
│   └── Vault.cs
│
├── ViewModels/
│   ├── Base/
│   ├── LoginViewModel.cs
│   ├── MainViewModel.cs
│   ├── PasswordDetailsViewModel.cs
│   └── PasswordGeneratorViewModel.cs
│
├── Views/
│   ├── LoginView.xaml
│   ├── MainView.xaml
│   ├── PasswordDetailsView.xaml
│   └── PasswordGeneratorView.xaml
│
├── Services/
│   ├── Authentication/
│   ├── Encryption/
│   ├── Vault/
│   ├── Clipboard/
│   ├── PasswordGenerator/
│   └── AutoLock/
│
├── Commands/
│
├── Data/
│
├── Helpers/
│
├── Resources/
│
├── Tests/
│
├── App.xaml
├── App.xaml.cs
└── PasswordManager.csproj
```

The structure may evolve as the application grows.

If changing the architecture, explain why before making a significant structural change.

---

# 5. Security Rules

Security-sensitive code requires additional caution.

## Master Password

NEVER store the master password in plaintext.

Never place it in:

* Source code
* JSON
* XML
* Configuration files
* Logs
* Database
* Comments
* Debug output

Never implement authentication using hard-coded passwords.

Bad:

```csharp
if (password == "admin123")
{
    Unlock();
}
```

---

## Key Derivation

Derive encryption keys from the master password using an appropriate password-based key derivation mechanism.

Use:

* Cryptographically random salt
* Appropriate KDF parameters
* Versioned cryptographic configuration

Do not use a simple hash such as:

```csharp
SHA256(masterPassword)
```

as the complete password-to-key solution.

---

## Encryption

Vault data stored on disk must be encrypted.

Use authenticated encryption such as:

```text
AES-GCM
```

Use a cryptographically random nonce for every encryption operation.

Never reuse an AES-GCM nonce with the same key.

The vault format should be versioned.

Conceptually:

```text
Vault Header
│
├── Version
├── Salt
├── Nonce
├── Authentication Tag
└── Ciphertext
```

Do not invent cryptographic algorithms.

Use established .NET cryptographic primitives.

---

# 6. Sensitive Data Rules

Never log:

```text
Master password
Stored passwords
Encryption keys
Vault plaintext
Sensitive credentials
```

Never include sensitive values in exception messages.

Never print secrets during debugging.

Avoid unnecessary copies of sensitive strings/data.

Keep decrypted vault data in memory only for as long as reasonably necessary.

When locking the vault, release sensitive state that is no longer needed.

---

# 7. Password Generation

Password generation must use a cryptographically secure random number generator.

Do NOT use:

```csharp
new Random()
```

for password generation.

Use appropriate APIs from:

```text
System.Security.Cryptography
```

The generator should support:

* Length
* Uppercase
* Lowercase
* Numbers
* Symbols

Validate generator settings before generating a password.

---

# 8. Clipboard Security

Clipboard operations must be isolated behind a clipboard service.

Support:

```text
Copy Username
Copy Password
```

Copied passwords should automatically be cleared after a configurable timeout.

Important:

Before clearing the clipboard, verify that the clipboard still contains the value placed there by this application.

Do not blindly overwrite content another application placed on the clipboard after the password was copied.

---

# 9. Auto-Lock

Implement inactivity tracking through a dedicated service.

The service should:

* Track user activity
* Reset the inactivity timer
* Trigger locking after timeout
* Support configurable timeout
* Support manual locking
* Stop/reset appropriately when application state changes

Do not scatter timer logic across multiple ViewModels.

---

# 10. Persistence

ViewModels must not directly perform file persistence.

Avoid:

```csharp
File.ReadAllText(...)
File.WriteAllText(...)
```

inside ViewModels.

Use:

```text
ViewModel
    ↓
VaultService
    ↓
VaultStorage
    ↓
Encrypted file
```

The storage layer should be responsible for:

* Reading vault data
* Writing vault data
* Handling file paths
* File operations
* Storage-specific exceptions

Encryption responsibilities should remain in the encryption service.

---

# 11. Error Handling

Never silently swallow exceptions.

Bad:

```csharp
try
{
    ...
}
catch
{
}
```

Handle expected failures appropriately.

Differentiate between:

* User input errors
* Authentication failures
* Vault corruption
* File-system failures
* Encryption failures
* Unexpected application failures

Do not expose internal technical details to normal users.

For example, prefer:

```text
Unable to unlock the vault.
The password may be incorrect or the vault may be corrupted.
```

instead of exposing internal cryptographic exception details.

---

# 12. Logging

Use structured application logging where appropriate.

Good events to log:

```text
Application started
Vault opened
Vault saved
Vault locked
Unexpected application error
```

Never log sensitive values.

Logging must never contain:

```text
Passwords
Master password
Encryption keys
Plaintext vault contents
```

---

# 13. Validation

Validation should exist at appropriate layers.

UI validation should provide useful feedback.

Important business/security validation should not rely exclusively on the UI.

Validate things such as:

* Empty required fields
* Master password strength
* Master password confirmation
* Invalid generator configuration
* Invalid vault state

---

# 14. Testing

Design services so they can be unit tested.

Prioritize tests for security-sensitive components.

At minimum test:

### Encryption

```text
Encrypt → Decrypt → Original data
```

### Wrong password

```text
Wrong key/password → Decryption failure
```

### Tampering

```text
Modified ciphertext → Authentication failure
```

### Password generation

Verify:

* Requested length
* Character requirements
* Invalid configurations
* Randomness behavior where practical

### Vault

Test:

* Create
* Load
* Save
* Update
* Delete
* Corrupted vault

### Auto-lock

Test:

* Timer behavior
* Activity reset
* Timeout
* Manual lock

### Clipboard

Test:

* Copy
* Timeout
* Clipboard changed externally

---

# 15. Development Workflow

Do NOT attempt to build the entire application in one step.

Build incrementally.

Follow:

```text
Phase 1
Project Foundation
        ↓
Phase 2
MVVM Foundation
        ↓
Phase 3
Password CRUD
        ↓
Phase 4
Master Password
        ↓
Phase 5
Encryption
        ↓
Phase 6
Search & Categories
        ↓
Phase 7
Password Generator
        ↓
Phase 8
Clipboard
        ↓
Phase 9
Auto-Lock
        ↓
Phase 10
UI Polish
        ↓
Phase 11
Security Hardening
        ↓
Phase 12
Testing
```

Do not jump ahead unless explicitly requested.

---

# 16. Teaching Mode

The developer is using this project to learn WPF and .NET.

Therefore, when implementing a feature:

### First explain

* What we are building
* Why it is needed
* Where it belongs architecturally
* What .NET/WPF concept it demonstrates

### Then implement

Show:

1. File path
2. Complete file contents
3. Required modifications
4. Explanation of important code

### Then test

Explain:

* How to run it
* What should happen
* How to verify it works
* Common mistakes

Do not simply provide unexplained code.

---

# 17. File Modification Rules

When modifying an existing file:

Prefer providing the **complete updated file** rather than an isolated snippet.

Clearly identify:

```text
CREATE:
path/to/NewFile.cs

MODIFY:
path/to/ExistingFile.cs
```

Do not modify unrelated files.

Keep changes focused on the current task.

---

# 18. Dependency Rules

Before adding a NuGet package:

1. Check whether .NET/WPF already provides the required functionality.
2. If built-in functionality is insufficient, consider a package.
3. Explain why the package is necessary.
4. Avoid unnecessary dependencies.

The fewer dependencies the application has, the easier it is to audit and maintain.

---

# 19. UI Principles

The application should have a clean modern desktop UI.

Prioritize:

* Clear navigation
* Consistent spacing
* Readable typography
* Accessible controls
* Clear validation messages
* Clear error messages
* Keyboard usability
* Appropriate password masking
* Confirmation for destructive operations

Avoid unnecessary visual complexity.

---

# 20. Production Mindset

Although this is a learning project, write the code with production-quality practices.

Avoid:

* Hard-coded secrets
* Magic strings
* Duplicate logic
* God classes
* Massive ViewModels
* Business logic in XAML
* Business logic in code-behind
* Unnecessary global state
* Silent exception handling
* Insecure cryptographic implementations

Prefer:

* Clear responsibilities
* Small focused classes
* Dependency injection
* Interfaces where useful
* Testable services
* Meaningful names
* Strong validation
* Secure defaults
* Explicit error handling

---

# 21. Security Decision Rule

Whenever implementing something security-sensitive, stop and evaluate:

```text
Is this secure?
Is this necessary?
Is there a standard .NET API for this?
Could this leak sensitive information?
Can this be tested?
```

If there is uncertainty around a cryptographic or security decision, do not guess.

Explain the uncertainty and verify the correct modern .NET approach before implementing it.

---

# 22. Do Not Make Unrequested Changes

Do not:

* Rewrite unrelated code
* Change architecture unnecessarily
* Add unrelated features
* Add unnecessary dependencies
* Change naming conventions without reason
* Refactor large sections unrelated to the current task

Keep each development step focused.

---

# 23. Current Development Rule

At the beginning of the project, start with:

```text
Phase 1 — Project Foundation
```

Only implement the current phase.

After completing a phase:

1. Explain what was completed.
2. Explain how to verify it.
3. Identify any important architectural decisions.
4. Wait for the developer to request the next phase.

Do not automatically continue to the next phase.

---

# 24. Final Principle

The goal is not merely:

> "Make the application work."

The goal is:

> **Build a secure, maintainable WPF application while teaching the developer how and why each part works.**

Always prioritize:

```text
Correctness
   +
Security
   +
Maintainability
   +
Testability
   +
Learning
```

Always read PROGRESS.md before starting work. After completing a task, update PROGRESS.md to accurately reflect the current implementation state. Never mark a task complete unless it has actually been implemented and verified.