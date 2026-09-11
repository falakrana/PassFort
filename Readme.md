Now I have a complete understanding of the architecture. Here's my comprehensive data flow analysis:

---

# PasswordManager Data Flow Architecture Analysis

## 🏗️ **High-Level Architecture (Clean MVVM + DI)**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        APPLICATION LAYER (App.xaml.cs)                       │
│  ┌──────────────────┐  ┌──────────────────────────────────────────────────┐  │
│  │ ServiceContainer │  │ IServiceProvider (Microsoft.Extensions.DI)       │  │
│  │ (Composition)    │  │ - Singleton: Infrastructure Services             │  │
│  └──────────────────┘  │ - Transient: ViewModels & Views                  │  │
│                        └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                    ┌────────────────┼────────────────┐
                    ▼                ▼                ▼
         ┌─────────────────┐ ┌─────────────┐ ┌──────────────────┐
         │   VIEW LAYER    │ │  VIEW MODEL │ │   SERVICE LAYER  │
         │  (XAML + CS)    │ │  (Logic)    │ │  (Interfaces)    │
         └─────────────────┘ └─────────────┘ └──────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
          ┌───────────────┐ ┌─────────────┐ ┌───────────────┐
          │ Authentication│ │ Encryption  │ │   Storage     │
          │   Service     │ │   Service   │ │  (File/I/O)   │
          └───────────────┘ └─────────────┘ └───────────────┘
```

---

## 🔐 **Authentication & Key Derivation Flow**

### 1. **First Run - Vault Initialization**
```
User Input (Master Password)
        │
        ▼
LoginViewModel.SetupCommand
        │
        ▼
AuthenticationService.InitializeMasterPassword(password, confirm)
        │
        ├─► Validate: length ≥ 8, passwords match
        │
        ├─► Generate Salt: RandomNumberGenerator.GetBytes(16)
        │
        ├─► Derive Key: PBKDF2-SHA256, 100,000 iterations → 32-byte key
        │       AesGcmEncryptionService.DeriveKey(password, salt)
        │
        ├─► Store: _activeSalt, _activeKey, _isUnlocked = true
        │
        ├─► Raise LockStateChanged event
        │
        └─► LoginViewModel.Authenticated event → MainViewModel.OnAuthenticated()
                │
                ▼
         EncryptedPasswordService.LoadVault() (triggered by LockStateChanged)
                │
                ├─► Vault doesn't exist → SeedInitialSampleEntries()
                │
                └─► SaveVault() → Encrypt + Write to disk
```

### 2. **Subsequent Runs - Vault Unlock**
```
User Input (Master Password)
        │
        ▼
LoginViewModel.UnlockCommand
        │
        ▼
AuthenticationService.Unlock(password)
        │
        ├─► Read vault file (FileVaultStorage.ReadVault())
        │       │
        │       └─► Binary format: Magic(4) + Version(4) + Salt(len+data) + 
        │           Nonce(len+data) + Tag(len+data) + Ciphertext(len+data)
        │
        ├─► Derive candidateKey: PBKDF2(password, storedSalt)
        │
        ├─► Decrypt vault payload → validates AES-GCM auth tag
        │       │
        │       └─► Success: _activeKey = candidateKey, _activeSalt = storedSalt
        │           _isUnlocked = true, Raise LockStateChanged
        │
        └─► Failure: Clear candidateKey, return error
```

---

## 📦 **Vault Data Flow (EncryptedPasswordService)**

### **In-Memory State**
```
EncryptedPasswordService
├── _entries: List<PasswordEntry> (PLAINTEXT - only when unlocked)
├── _entriesLock: object (for thread-safe ObservableCollection sync)
└── Subscribes to: AuthenticationService.LockStateChanged
```

### **CRUD Operations Flow**

| Operation | Flow |
|-----------|------|
| **GetAll()** | `EnsureUnlocked()` → Return `_entries.Select(e => e.Clone())` |
| **GetById(id)** | `EnsureUnlocked()` → Find + `Clone()` |
| **Add(entry)** | `EnsureUnlocked()` → Clone + timestamps → `_entries.Add()` → `SaveVault()` |
| **Update(entry)** | `EnsureUnlocked()` → Find index → Clone + timestamp → `_entries[i]=updated` → `SaveVault()` |
| **Delete(id)** | `EnsureUnlocked()` → `_entries.RemoveAll()` → `SaveVault()` |

### **SaveVault() - Critical Encryption Pipeline**
```
_passwordService.Add/Update/Delete()
        │
        ▼
EncryptedPasswordService.SaveVault()
        │
        ├─► EnsureUnlocked() → throws if locked
        │
        ├─► Get ActiveKey + ActiveSalt from AuthService
        │
        ├─► Serialize: JsonSerializer.SerializeToUtf8Bytes(_entries)
        │
        ├─► Encrypt: AesGcmEncryptionService.Encrypt(plaintext, key, salt)
        │       │
        │       ├─► Generate 12-byte nonce (RandomNumberGenerator)
        │       ├─► AES-256-GCM encrypt → ciphertext + 16-byte tag
        │       └─► Return EncryptedPayload {Salt, Nonce, Tag, Ciphertext}
        │
        ├─► Zero out plaintextBytes (Array.Clear) ← **Memory hygiene**
        │
        └─► FileVaultStorage.WriteVault(payload)
                │
                ├─► Write to .tmp file (atomic)
                ├─► Magic + Version + Length-prefixed components
                ├─► Flush to disk (FileStream.Flush(true))
                └─► File.Move(tmp → vault.dat, overwrite: true) ← **Crash-safe**
```

### **LoadVault() - Decryption Pipeline**
```
EncryptedPasswordService.LoadVault() (on unlock)
        │
        ├─► FileVaultStorage.ReadVault()
        │       └─► Parse binary → EncryptedPayload
        │
        ├─► Get ActiveKey from AuthService
        │
        ├─► Decrypt: AesGcmEncryptionService.Decrypt(payload, key)
        │       └─► AES-GCM Decrypt + Auth Tag validation
        │           └─► Throws CryptographicException if tampered/wrong key
        │
        ├─► Deserialize: JsonSerializer.Deserialize<List<PasswordEntry>>
        │
        ├─► _entries.AddRange(loadedEntries)
        │
        └─► Zero out plaintextBytes (Array.Clear) ← **Memory hygiene**
```

### **Lock State Transition**
```
AuthenticationService.Lock()
        │
        ├─► Array.Clear(_activeKey) → _activeKey = null
        ├─► _activeSalt = null
        ├─► _isUnlocked = false
        └─► Raise LockStateChanged
                │
                ▼
EncryptedPasswordService.OnLockStateChanged()
        │
        └─► _entries.Clear() ← **Wipe plaintext from memory**
```

---

## 🖥️ **UI Data Flow (MVVM Pattern)**

### **View Binding Hierarchy**
```
MainWindow (DataContext = MainViewModel)
    │
    ├─► MainView (ContentControl)
    │       │
    │       ├─► LoginView (when !IsVaultUnlocked)
    │       │       DataContext = MainViewModel.LoginViewModel
    │       │
    │       └─► Vault Workspace (when IsVaultUnlocked)
    │               ├─► Left Sidebar: FilteredEntries (ICollectionView)
    │               ├─► Right Panel: SelectedEntry (Detail View) 
    │               │                           OR EditingEntry (Add/Edit Form)
    │               ├─► Embedded: PasswordGeneratorViewModel
    │               └─► Settings Overlay: Change Master Password
    │
    └─► Input Events → MainViewModel.RegisterUserActivity() → AutoLockService
```

### **Key Data Bindings**
```
MainViewModel Properties → UI
├── PasswordEntries (ObservableCollection) → ListBox.ItemsSource
├── FilteredEntries (ICollectionView) → Filtered ListBox
├── SelectedEntry → Detail View / Commands CanExecute
├── EditingEntry → Add/Edit Form fields
├── SearchText → ICollectionView.Refresh() (live filter)
├── SelectedCategoryFilter → ICollectionView.Refresh() (live filter)
├── IsVaultUnlocked → Visibility converters (entire UI sections)
├── IsAdding/IsEditing → Form vs Detail View toggles
├── IsPasswordVisible → Mask/Unmask password TextBlocks
└── StatusMessage → Footer status bar
```

### **Command Execution Flow (Example: Save)**
```
User clicks "SAVE PASSWORD"
        │
        ▼
MainViewModel.SaveCommand (RelayCommand)
        │
        ├─► CanExecuteSave() → IsVaultUnlocked && (IsAdding||IsEditing) && EditingEntry!=null
        │
        ▼
ExecuteSave()
        │
        ├─► Validate: Title required, Password required
        │
        ├─► IsBusy = true
        │
        ├─► If IsAdding: _passwordService.Add(EditingEntry)
        │   If IsEditing: _passwordService.Update(EditingEntry)
        │
        ├─► LoadEntries() → Refresh ObservableCollection from service
        │
        ├─► Select saved entry in list
        │
        └─► IsBusy = false
```

---

## 🔄 **Auto-Lock Flow**

```
MainWindow Input Events (MouseMove, KeyDown, MouseDown)
        │
        ▼
MainWindow.OnUserActivity() → MainViewModel.RegisterUserActivity()
        │
        ▼
AutoLockService.RegisterActivity()
        │
        ├─► If running & enabled & unlocked → ResetTimer()
        │       └─► _timer.Change(_timeout, Infinite)
        │
        ▼ (after 5 min default inactivity)
AutoLockService.OnTimerElapsed()
        │
        ├─► StopInternal()
        │
        ├─► _authService.Lock() → Clears keys, raises LockStateChanged
        │
        └─► Raise AutoLocked event
                │
                ▼
MainViewModel.OnAutoLocked() → StatusMessage = "Vault automatically locked..."
```

---

## 📋 **Clipboard Security Flow**

```
User clicks "COPY PASSWORD"
        │
        ▼
MainViewModel.CopyPasswordCommand
        │
        ▼
ClipboardService.CopySensitiveToClipboard(password)
        │
        ├─► SetClipboardTextInternal() (STA thread)
        │
        ├─► Store _lastCopiedSensitiveText = password
        │
        ├─► Start Timer (30s default)
        │       │
        │       └─► OnTimerElapsed → ClearIfMatches(password)
        │               │
        │               ├─► If clipboard still matches → Clear + ClipboardCleared event
        │               └─► If changed externally → Do nothing
        │
        └─► StatusMessage with timeout info
```

---

## 🎯 **Password Generator Flow**

```
MainViewModel.GeneratePasswordForEntryCommand
        │
        ▼
PasswordGeneratorViewModel.GeneratePassword()
        │
        ├─► BuildOptions() → PasswordGeneratorOptions
        │
        ├─► _generatorService.ValidateOptions()
        │
        ├─► _generatorService.GeneratePassword(options)
        │       └─► Cryptographically random character selection
        │
        ├─► GeneratedPassword = result
        ├─► PasswordGeneratedAndSelected event
        │
        ▼
MainViewModel.ExecuteGeneratePasswordForEntry()
        │
        └─► EditingEntry.Password = GeneratedPassword
```

---

## 🧪 **Dependency Injection Configuration (App.xaml.cs)**

```csharp
// Infrastructure (Singleton - stateless/thread-safe)
services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
services.AddSingleton<IVaultStorage, FileVaultStorage>();
services.AddSingleton<IClipboardService, ClipboardService>();
services.AddSingleton<IAutoLockService, AutoLockService>();

// Domain (Singleton - maintain state)
services.AddSingleton<IAuthenticationService, AuthenticationService>();
services.AddSingleton<IPasswordService, EncryptedPasswordService>();
services.AddSingleton<IPasswordGeneratorService, PasswordGeneratorService>();

// ViewModels (Transient - new instance per resolution)
services.AddTransient<PasswordGeneratorViewModel>();
services.AddTransient<MainViewModel>();

// Views (Transient - with DI injection)
services.AddTransient<MainWindow>(sp => new MainWindow
{
    DataContext = sp.GetRequiredService<MainViewModel>()
});
```

---

## 🔑 **Security Design Highlights**

| Concern | Implementation |
|---------|----------------|
| **Key Derivation** | PBKDF2-HMAC-SHA256, 100k iterations, 16-byte salt |
| **Encryption** | AES-256-GCM (authenticated encryption) |
| **Memory Hygiene** | `Array.Clear()` on all sensitive byte arrays (keys, plaintext) |
| **Constant-Time Compare** | `CryptographicOperations.FixedTimeEquals()` for auth verification |
| **Atomic Writes** | Write to `.tmp` → `File.Move(overwrite:true)` |
| **Vault Format** | Magic header + Version + Length-prefixed components |
| **Auto-Lock** | 5-min default, activity reset, clipboard auto-clear (30s) |
| **PasswordBox Binding** | Attached property helper (secure binding) |
| **Master Password Change** | Re-encrypt entire vault with new key + fresh salt |

---

## 📁 **File Structure Summary**

```
PasswordManager/
├── App.xaml.cs                    # DI Composition Root + Test Runner
├── MainWindow.xaml(.cs)           # Shell + Input activity tracking
├── Models/
│   ├── PasswordEntry.cs           # Domain entity (with Clone())
│   └── Category.cs                # Static category lists
├── ViewModels/
│   ├── Base/ViewModelBase.cs      # INotifyPropertyChanged base
│   ├── MainViewModel.cs           # Primary orchestrator (700+ lines)
│   ├── LoginViewModel.cs          # Auth flow (setup/unlock)
│   └── PasswordGeneratorViewModel.cs
├── Views/
│   ├── MainView.xaml              # Master-detail workspace
│   └── LoginView.xaml             # Auth UI
├── Services/
│   ├── Authentication/            # Master password, key derivation, lock state
│   ├── Encryption/                # PBKDF2 + AES-256-GCM
│   ├── Vault/                     # IPasswordService + Storage
│   ├── Clipboard/                 # Secure clipboard with auto-clear
│   ├── AutoLock/                  # Inactivity monitoring
│   ├── UI/                        # IDialogService
│   └── PasswordGenerator/         # Generation logic
├── Commands/RelayCommand.cs       # ICommand implementation
└── Helpers/                       # PasswordBox binding + Converters
```

---

## ⚠️ **Architectural Observations (Senior Dev Perspective)**

**Strengths:**
- Clean separation via interfaces + DI
- Proper memory hygiene (Array.Clear on sensitive data)
- AES-GCM authenticated encryption (not just AES-CBC)
- Atomic file writes prevent corruption
- Thread-safe ObservableCollection with BindingOperations.EnableCollectionSynchronization
- Event-driven lock state propagation
- Comprehensive test coverage (10 test phases)

**Areas for Consideration:**
1. **MainViewModel is large (700+ lines)** - Could extract sub-ViewModels (EntryDetailVM, SettingsVM)
2. **No async/await in services** - File I/O and crypto are sync; could block UI on large vaults
3. **Single vault file** - No multi-vault support
4. **No password strength meter** in UI
5. **Hardcoded 100k PBKDF2 iterations** - Consider making configurable for future hardware
6. **No backup/export functionality**

---

Would you like me to elaborate on any specific flow, or do you have questions about implementation details?