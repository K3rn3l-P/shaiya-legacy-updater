#define NOMINMAX
#include <windows.h>
#include <commdlg.h>
#include <bcrypt.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <string>
#include <vector>

#ifdef max
#undef max
#endif
#ifdef min
#undef min
#endif

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "Comdlg32.lib")

namespace fs = std::filesystem;

static constexpr uint16_t kFormatVersion = 1;
static constexpr uint16_t kFlagAes256Gcm = 1;
static constexpr size_t kNonceSize = 12;
static constexpr size_t kTagSize   = 16;
static constexpr size_t kKeySize   = 32;

#pragma pack(push, 1)
struct SahEncHeaderV1
{
    char magic[4];
    uint16_t version;
    uint16_t flags;
    uint64_t plainSize;
    uint8_t nonce[kNonceSize];
    uint8_t tag[kTagSize];
    uint8_t reserved[20];
};
#pragma pack(pop)
static_assert(sizeof(SahEncHeaderV1) == 64, "Unexpected header size");

enum class Mode { Encrypt, Decrypt };

/* ── Utility ──────────────────────────────────────────── */

bool NtOk(NTSTATUS s) { return s >= 0; }

std::string NtStr(NTSTATUS s)
{
    std::ostringstream o;
    o << "NTSTATUS=0x" << std::hex << std::uppercase << static_cast<unsigned long>(s);
    return o.str();
}

void PauseAtEnd()
{
    std::cout << "\nPremi Invio per chiudere...";
    std::cin.clear();
    std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    std::cin.get();
}

std::string PathToStr(const fs::path& p) { return p.u8string(); }

/* ── INI helpers ──────────────────────────────────────── */

fs::path GetExeDir()
{
    wchar_t buf[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, buf, MAX_PATH);
    return fs::path(buf).parent_path();
}

bool ReadIniValue(const fs::path& iniPath, const std::string& key, std::string& outVal)
{
    std::ifstream f(iniPath);
    if (!f) return false;

    std::string line;
    while (std::getline(f, line))
    {
        const auto eq = line.find('=');
        if (eq == std::string::npos) continue;

        std::string k = line.substr(0, eq);
        while (!k.empty() && (k.front() == ' ' || k.front() == '\t')) k.erase(k.begin());
        while (!k.empty() && (k.back()  == ' ' || k.back()  == '\t' || k.back() == '\r')) k.pop_back();

        if (k != key) continue;

        std::string v = line.substr(eq + 1);
        while (!v.empty() && (v.front() == ' ' || v.front() == '\t')) v.erase(v.begin());
        while (!v.empty() && (v.back()  == ' ' || v.back()  == '\t' || v.back() == '\r')) v.pop_back();

        if (!v.empty()) { outVal = v; return true; }
    }
    return false;
}

void WriteIniValue(const fs::path& iniPath, const std::string& key, const std::string& val)
{
    std::vector<std::string> lines;
    bool found = false;
    {
        std::ifstream f(iniPath);
        std::string line;
        while (std::getline(f, line))
        {
            const auto eq = line.find('=');
            if (eq != std::string::npos)
            {
                std::string k = line.substr(0, eq);
                while (!k.empty() && (k.back() == ' ' || k.back() == '\t' || k.back() == '\r')) k.pop_back();
                if (k == key)
                {
                    lines.push_back(key + "=" + val);
                    found = true;
                    continue;
                }
            }
            if (!line.empty() && line.back() == '\r') line.pop_back();
            lines.push_back(line);
        }
    }
    if (!found) lines.push_back(key + "=" + val);

    std::ofstream f(iniPath, std::ios::trunc);
    for (const auto& l : lines) f << l << "\n";
}

bool HexToBytes(const std::string& hex, std::array<uint8_t, kKeySize>& out, std::string& error)
{
    if (hex.size() != kKeySize * 2)
    {
        error = "SahKeyHex deve essere esattamente " + std::to_string(kKeySize * 2) +
                " caratteri hex (trovati " + std::to_string(hex.size()) + ").";
        return false;
    }
    for (size_t i = 0; i < kKeySize; ++i)
    {
        auto digit = [](char c, uint8_t& d) -> bool {
            if (c >= '0' && c <= '9') { d = static_cast<uint8_t>(c - '0'); return true; }
            if (c >= 'a' && c <= 'f') { d = static_cast<uint8_t>(c - 'a' + 10); return true; }
            if (c >= 'A' && c <= 'F') { d = static_cast<uint8_t>(c - 'A' + 10); return true; }
            return false;
        };
        uint8_t hi = 0, lo = 0;
        if (!digit(hex[i * 2], hi) || !digit(hex[i * 2 + 1], lo))
        {
            error = "Carattere hex non valido in SahKeyHex alla posizione " + std::to_string(i * 2);
            return false;
        }
        out[i] = static_cast<uint8_t>((hi << 4) | lo);
    }
    return true;
}

/* ── sahcrypt.ini bootstrap ───────────────────────────── */

// Trova la chiave SSH di default del sistema (%USERPROFILE%\.ssh\id_ed25519 o id_rsa)
static std::string DetectDefaultSshKey()
{
    char profile[MAX_PATH] = {};
    if (GetEnvironmentVariableA("USERPROFILE", profile, MAX_PATH) == 0) return {};

    const fs::path base = fs::path(profile) / ".ssh";
    for (const auto& name : { "id_ed25519", "id_rsa", "id_ecdsa" })
    {
        fs::path p = base / name;
        if (fs::exists(p)) return PathToStr(p);
    }
    return {};
}

void CreateDefaultSahcryptIni(const fs::path& iniPath)
{
    if (fs::exists(iniPath)) return;

    std::string defaultKey = DetectDefaultSshKey();

    std::ofstream f(iniPath);
    if (!f) return;

    f << "; sahcrypt.ini - configurazione SahCrypt Tool\n"
      << "; Generato automaticamente.\n"
      << "\n"
      << "; === Connessione SSH al server (stessi campi di patchmaker.ini) ===\n"
      << "SshHost=10.10.5.91\n"
      << "SshUser=administrator\n"
      << "\n"
      << "; Password SSH - come PatchMaker (richiede plink.exe di PuTTY)\n"
      << "SshPassword=\n"
      << "\n"
      << "; Chiave SSH privata - alternativa a SshPassword, usa ssh.exe built-in\n";
    if (!defaultKey.empty())
        f << "SshKeyPath=" << defaultKey << "\n";
    else
        f << "; SshKeyPath=C:\\Users\\utente\\.ssh\\id_ed25519\n"
          << "SshKeyPath=\n";
    f << "\n"
      << "; Path del GMSecurity.ini sul server (stesso default di PatchMaker)\n"
      << "SshRemotePath=C:\\ShaiyaServer\\PSM_Client\\Bin\\Data\\GMSecurity.ini\n"
      << "\n"
      << "; === Chiave cached - scritta automaticamente dopo SSH fetch ===\n"
      << "; SahKeyHex=\n";

    if (!defaultKey.empty())
        std::cout << "[info] Creato sahcrypt.ini. Chiave SSH auto-rilevata: " << defaultKey << "\n"
                  << "[info] Compila SshPassword o verifica SshKeyPath, poi premi F).\n";
    else
        std::cout << "[info] Creato sahcrypt.ini. Compila SshPassword o SshKeyPath, poi premi F).\n";
}

/* ── SSH fetch ────────────────────────────────────────── */

struct SshSettings
{
    std::string host;
    std::string user;
    std::string password;    // plink.exe password auth
    std::string keyPath;     // ssh.exe -i key auth
    std::string remotePath;  // path a GMSecurity.ini sul server
    std::string sahKeyName;  // nome chiave nel file remoto (default SahCryptKeyHex)
};

bool ReadSshSettings(const fs::path& iniPath, SshSettings& out, std::string& error)
{
    out.sahKeyName = "SahCryptKeyHex";

    ReadIniValue(iniPath, "SshHost",       out.host);
    ReadIniValue(iniPath, "SshUser",       out.user);
    ReadIniValue(iniPath, "SshPassword",   out.password);
    ReadIniValue(iniPath, "SshKeyPath",    out.keyPath);
    ReadIniValue(iniPath, "SshRemotePath", out.remotePath);
    ReadIniValue(iniPath, "SahKeyName",    out.sahKeyName);

    if (out.host.empty() || out.user.empty() || out.remotePath.empty())
    {
        error =
            "sahcrypt.ini: SSH non configurato.\n"
            "  Aggiungi:\n"
            "    SshHost=<ip>\n"
            "    SshUser=<utente>\n"
            "    SshRemotePath=<path GMSecurity.ini sul server>\n"
            "    SshKeyPath=<path chiave privata>  ; oppure SshPassword=<password>";
        return false;
    }
    if (out.keyPath.empty() && out.password.empty())
    {
        error = "sahcrypt.ini: specificare SshKeyPath (ssh.exe) oppure SshPassword (plink.exe).";
        return false;
    }
    return true;
}

// Converte path SFTP-style (/C:/...) o forward-slash a backslash
static std::string ToWindowsCmdPath(std::string p)
{
    if (p.size() >= 3 && p[0] == '/' && std::isalpha((unsigned char)p[1]) && p[2] == ':')
        p = p.substr(1);
    std::replace(p.begin(), p.end(), '/', '\\');
    return p;
}

// Esegue exe con args, cattura stdout+stderr via pipe.
// Bypassa cmd.exe completamente: niente quoting hell, niente WoW64 tramite _popen.
static std::string RunProcessCapture(const std::string& exe, const std::string& args,
                                     int& exitCode, std::string& launchError)
{
    std::string cmdLine = "\"" + exe + "\" " + args;

    HANDLE hR = INVALID_HANDLE_VALUE, hW = INVALID_HANDLE_VALUE;
    SECURITY_ATTRIBUTES sa{sizeof(sa), nullptr, TRUE};
    if (!CreatePipe(&hR, &hW, &sa, 0))
    {
        launchError = "CreatePipe failed: " + std::to_string(GetLastError());
        return {};
    }
    SetHandleInformation(hR, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOA si{};
    si.cb         = sizeof(si);
    si.dwFlags    = STARTF_USESTDHANDLES;
    si.hStdOutput = hW;
    si.hStdError  = hW;
    si.hStdInput  = INVALID_HANDLE_VALUE;

    PROCESS_INFORMATION pi{};
    // lpApplicationName=NULL: Windows parsifica exe dalla cmdLine, gestisce Sysnative/spazi
    const BOOL ok = CreateProcessA(
        nullptr, const_cast<LPSTR>(cmdLine.c_str()),
        nullptr, nullptr, TRUE, CREATE_NO_WINDOW,
        nullptr, nullptr, &si, &pi);

    CloseHandle(hW);

    if (!ok)
    {
        CloseHandle(hR);
        launchError = "Impossibile avviare " + exe +
                      " (errore=" + std::to_string(GetLastError()) + ").";
        return {};
    }

    std::string output;
    char buf[4096];
    DWORD n = 0;
    while (ReadFile(hR, buf, sizeof(buf), &n, nullptr) && n > 0)
        output.append(buf, n);

    CloseHandle(hR);
    WaitForSingleObject(pi.hProcess, 30000);

    DWORD ec = 0;
    GetExitCodeProcess(pi.hProcess, &ec);
    exitCode = static_cast<int>(ec);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    return output;
}

// Cerca ssh.exe in piu' posizioni. Git for Windows e' il fallback piu' probabile
// quando OpenSSH non e' in PATH (es. installato ma non aggiunto al PATH di cmd.exe).
static std::string FindSshExe(std::string& notFoundError)
{
    char pf[MAX_PATH]   = {}, pf86[MAX_PATH] = {}, wd[MAX_PATH] = {};
    ExpandEnvironmentStringsA("%ProgramFiles%",      pf,   MAX_PATH);
    ExpandEnvironmentStringsA("%ProgramFiles(x86)%", pf86, MAX_PATH);
    GetWindowsDirectoryA(wd, MAX_PATH);
    std::string wdStr(wd);

    std::vector<std::string> candidates = {
        // Git for Windows — path normale, nessun WoW64, fs::exists funziona
        std::string(pf)   + "\\Git\\usr\\bin\\ssh.exe",
        std::string(pf)   + "\\Git\\bin\\ssh.exe",
        std::string(pf86) + "\\Git\\usr\\bin\\ssh.exe",
        // OpenSSH via Sysnative: bypassa WoW64 redirect per processi 32-bit
        // (fs::exists puo' fallire, ma CreateProcess lo risolve correttamente)
        wdStr + "\\Sysnative\\OpenSSH\\ssh.exe",
        // OpenSSH diretto (funziona per build x64 o se non rediretto)
        wdStr + "\\System32\\OpenSSH\\ssh.exe",
    };

    for (const auto& c : candidates)
    {
        std::error_code ec;
        if (fs::exists(c, ec) && !ec) return c;
    }

    // Sysnative: fs::exists puo' mentire da processo 32-bit.
    // Prova a lanciarlo con -V tramite CreateProcess (che risolve Sysnative correttamente).
    {
        std::string sysnative = wdStr + "\\Sysnative\\OpenSSH\\ssh.exe";
        int ec2 = -1;
        std::string dummy;
        RunProcessCapture(sysnative, "-V", ec2, dummy);
        if (dummy.empty() && ec2 >= 0) return sysnative; // se CreateProcess ha avuto successo
        // se dummy non e' vuoto (output di -V) → trovato
        // nota: ssh -V stampa su stderr, quindi output non sara' vuoto se trovato
    }

    // Controllo Sysnative con output (ssh -V scrive su stderr → catturato in output)
    {
        std::string sysnative = wdStr + "\\Sysnative\\OpenSSH\\ssh.exe";
        int ec2 = -1;
        std::string launchErr;
        std::string out = RunProcessCapture(sysnative, "-V", ec2, launchErr);
        if (launchErr.empty()) return sysnative; // CreateProcess ok → exe esiste
    }

    notFoundError =
        "ssh.exe non trovato. Candidati provati:\n";
    for (const auto& c : candidates)
        notFoundError += "  - " + c + "\n";
    notFoundError +=
        "Soluzioni:\n"
        "  1) Installa Git for Windows (contiene ssh.exe in usr/bin)\n"
        "  2) Abilita 'OpenSSH Client' in Impostazioni -> App -> Funzionalita' facoltative\n"
        "  3) Aggiungi SahKeyHex=<64hex> manualmente in sahcrypt.ini";
    return {};
}

static std::string FindPlink()
{
    const std::vector<std::string> candidates = {
        "C:\\Program Files\\PuTTY\\plink.exe",
        "C:\\Program Files (x86)\\PuTTY\\plink.exe"
    };
    for (const auto& c : candidates)
    {
        std::error_code ec;
        if (fs::exists(c, ec) && !ec) return c;
    }
    return {};
}

std::string RunSshCommand(const SshSettings& ssh, std::string& error)
{
    std::string remotePath = ssh.remotePath;
    bool isWin = (remotePath.size() >= 2 && std::isalpha((unsigned char)remotePath[0]) && remotePath[1] == ':')
              || (remotePath.size() >= 3 && remotePath[0] == '/' && std::isalpha((unsigned char)remotePath[1]) && remotePath[2] == ':');

    std::string remoteCmd = isWin
        ? "type " + ToWindowsCmdPath(remotePath)
        : "cat " + remotePath;

    std::string exe, args;

    if (!ssh.keyPath.empty())
    {
        std::string findErr;
        exe = FindSshExe(findErr);
        if (exe.empty()) { error = findErr; return {}; }

        args  = "-i \"" + ssh.keyPath + "\"";
        args += " -o IdentitiesOnly=yes";
        args += " -o BatchMode=yes";
        args += " -o StrictHostKeyChecking=no";
        args += " " + ssh.user + "@" + ssh.host;
        args += " \"" + remoteCmd + "\"";
    }
    else if (!ssh.password.empty())
    {
        exe = FindPlink();
        if (exe.empty())
        {
            error = "SshPassword richiede plink.exe (PuTTY).\n"
                    "  Installa PuTTY oppure usa SshKeyPath con la tua chiave privata.";
            return {};
        }
        args  = "-batch -pw \"" + ssh.password + "\"";
        args += " " + ssh.user + "@" + ssh.host;
        args += " \"" + remoteCmd + "\"";
    }
    else
    {
        error = "Nessuna autenticazione SSH configurata. Imposta SshKeyPath o SshPassword.";
        return {};
    }

    int exitCode = 0;
    std::string launchErr;
    std::string output = RunProcessCapture(exe, args, exitCode, launchErr);

    if (!launchErr.empty()) { error = launchErr; return {}; }

    if (exitCode != 0)
    {
        error = "SSH fallito (exit=" + std::to_string(exitCode) + ").";
        if (!output.empty())
        {
            while (!output.empty() && (output.back() == '\n' || output.back() == '\r')) output.pop_back();
            error += "\n  " + output;
        }
        return {};
    }
    return output;
}

// Come ParseKeyFromIniContent di PatchMaker: accetta SahKeyHex o SahCryptKeyHex
static bool MatchesKeyName(const std::string& k)
{
    auto iequal = [](const std::string& a, const std::string& b) {
        if (a.size() != b.size()) return false;
        for (size_t i = 0; i < a.size(); ++i)
            if (std::tolower((unsigned char)a[i]) != std::tolower((unsigned char)b[i])) return false;
        return true;
    };
    return iequal(k, "SahKeyHex") || iequal(k, "SahCryptKeyHex");
}

bool FetchKeyFromSsh(const SshSettings& ssh, std::string& outHex, std::string& error)
{
    std::cout << "[ssh] Connessione a " << ssh.user << "@" << ssh.host << "...\n";

    const std::string content = RunSshCommand(ssh, error);
    if (content.empty()) return false;

    std::istringstream ss(content);
    std::string line;
    while (std::getline(ss, line))
    {
        const auto eq = line.find('=');
        if (eq == std::string::npos) continue;

        std::string k = line.substr(0, eq);
        while (!k.empty() && (k.front() == ' ' || k.front() == '\t')) k.erase(k.begin());
        while (!k.empty() && (k.back()  == ' ' || k.back()  == '\t' || k.back() == '\r')) k.pop_back();

        if (!MatchesKeyName(k)) continue;

        std::string v = line.substr(eq + 1);
        while (!v.empty() && (v.front() == ' ' || v.front() == '\t')) v.erase(v.begin());
        while (!v.empty() && (v.back()  == ' ' || v.back()  == '\t' || v.back() == '\r')) v.pop_back();

        if (!v.empty())
        {
            outHex = v;
            std::cout << "[ssh] Chiave trovata: " << k << "=" << outHex.substr(0, 8) << "...\n";
            return true;
        }
    }

    error = "Chiave SahKeyHex/SahCryptKeyHex non trovata in GMSecurity.ini remoto.";
    return false;
}

/* ── Key load ─────────────────────────────────────────── */

// Priorita':
//   1. SahKeyHex in sahcrypt.ini  (cache locale)
//   2. SahKeyHex in patchmaker.ini  (PatchMaker l'ha gia' recuperata via SSH)
//   3. SSH fetch da GMSecurity.ini usando le impostazioni SSH in sahcrypt.ini
bool LoadSahKey(std::array<uint8_t, kKeySize>& outKey, std::string& error,
                bool forceRefresh = false)
{
    const fs::path exeDir        = GetExeDir();
    const fs::path sahcryptIni   = exeDir / "sahcrypt.ini";
    const fs::path patchmakerIni = exeDir / "patchmaker.ini";

    CreateDefaultSahcryptIni(sahcryptIni);

    std::string hexVal;
    std::string source;

    if (!forceRefresh && ReadIniValue(sahcryptIni, "SahKeyHex", hexVal))
    {
        source = PathToStr(sahcryptIni);
    }
    else if (!forceRefresh && ReadIniValue(patchmakerIni, "SahKeyHex", hexVal))
    {
        source = PathToStr(patchmakerIni);
        WriteIniValue(sahcryptIni, "SahKeyHex", hexVal);
        std::cout << "[info] Chiave da patchmaker.ini, salvata in sahcrypt.ini\n";
    }
    else
    {
        SshSettings ssh;
        std::string sshErr;
        if (!ReadSshSettings(sahcryptIni, ssh, sshErr))
        {
            error =
                "SahKeyHex non trovata e SSH non configurato.\n\n" + sshErr +
                "\n\nModifica: " + PathToStr(sahcryptIni) +
                "\nPoi premi F) per scaricare la chiave dal server.";
            return false;
        }

        if (!FetchKeyFromSsh(ssh, hexVal, sshErr))
        {
            error = "SSH fetch fallito: " + sshErr +
                    "\n\nIn alternativa aggiungi manualmente:\n"
                    "  SahKeyHex=<64 caratteri hex>";
            return false;
        }

        WriteIniValue(sahcryptIni, "SahKeyHex", hexVal);
        std::cout << "[ssh] Chiave salvata in sahcrypt.ini\n";
        source = "SSH:" + ssh.host;
    }

    if (!HexToBytes(hexVal, outKey, error)) return false;
    std::cout << "[info] Chiave caricata da: " << source << "\n";
    return true;
}

/* ── File I/O ─────────────────────────────────────────── */

bool ReadFileBytes(const fs::path& path, std::vector<uint8_t>& out, std::string& error)
{
    std::ifstream file(path, std::ios::binary);
    if (!file) { error = "Impossibile aprire file: " + PathToStr(path); return false; }
    file.seekg(0, std::ios::end);
    const auto size = file.tellg();
    if (size < 0) { error = "Impossibile determinare dimensione: " + PathToStr(path); return false; }
    file.seekg(0, std::ios::beg);
    out.resize(static_cast<size_t>(size));
    if (!out.empty())
    {
        file.read(reinterpret_cast<char*>(out.data()), static_cast<std::streamsize>(out.size()));
        if (!file) { error = "Lettura file fallita: " + PathToStr(path); return false; }
    }
    return true;
}

bool WriteFileBytes(const fs::path& path, const std::vector<uint8_t>& data, std::string& error)
{
    std::ofstream file(path, std::ios::binary | std::ios::trunc);
    if (!file) { error = "Impossibile aprire output: " + PathToStr(path); return false; }
    if (!data.empty())
    {
        file.write(reinterpret_cast<const char*>(data.data()), static_cast<std::streamsize>(data.size()));
        if (!file) { error = "Scrittura fallita: " + PathToStr(path); return false; }
    }
    return true;
}

/* ── BCrypt AES-256-GCM ───────────────────────────────── */

bool GenRandomBytes(uint8_t* out, size_t size, std::string& error)
{
    const NTSTATUS s = BCryptGenRandom(nullptr, out, static_cast<ULONG>(size), BCRYPT_USE_SYSTEM_PREFERRED_RNG);
    if (!NtOk(s)) { error = "BCryptGenRandom failed: " + NtStr(s); return false; }
    return true;
}

bool OpenAesGcm(BCRYPT_ALG_HANDLE& hAlg, std::string& error)
{
    NTSTATUS s = BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_AES_ALGORITHM, nullptr, 0);
    if (!NtOk(s)) { error = "BCryptOpenAlgorithmProvider failed: " + NtStr(s); return false; }

    s = BCryptSetProperty(hAlg, BCRYPT_CHAINING_MODE,
        reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_GCM)),
        static_cast<ULONG>(sizeof(BCRYPT_CHAIN_MODE_GCM)), 0);
    if (!NtOk(s))
    {
        BCryptCloseAlgorithmProvider(hAlg, 0);
        error = "BCryptSetProperty(GCM) failed: " + NtStr(s);
        return false;
    }
    return true;
}

bool BuildAesKey(BCRYPT_ALG_HANDLE hAlg,
                 const std::array<uint8_t, kKeySize>& key,
                 BCRYPT_KEY_HANDLE& hKey,
                 std::vector<uint8_t>& keyObj,
                 std::string& error)
{
    DWORD objLen = 0, written = 0;
    NTSTATUS s = BCryptGetProperty(hAlg, BCRYPT_OBJECT_LENGTH,
        reinterpret_cast<PUCHAR>(&objLen), sizeof(objLen), &written, 0);
    if (!NtOk(s)) { error = "BCryptGetProperty(OBJECT_LENGTH) failed: " + NtStr(s); return false; }

    keyObj.resize(objLen);
    s = BCryptGenerateSymmetricKey(hAlg, &hKey, keyObj.data(), static_cast<ULONG>(keyObj.size()),
        const_cast<PUCHAR>(key.data()), static_cast<ULONG>(key.size()), 0);
    if (!NtOk(s)) { error = "BCryptGenerateSymmetricKey failed: " + NtStr(s); return false; }
    return true;
}

bool Aes256GcmEncrypt(const std::array<uint8_t, kKeySize>& key,
                      const std::vector<uint8_t>& plain,
                      const uint8_t nonce[kNonceSize],
                      std::vector<uint8_t>& cipher,
                      uint8_t tag[kTagSize],
                      std::string& error)
{
    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_KEY_HANDLE hKey = nullptr;
    std::vector<uint8_t> keyObj;

    if (!OpenAesGcm(hAlg, error)) return false;
    if (!BuildAesKey(hAlg, key, hKey, keyObj, error)) { BCryptCloseAlgorithmProvider(hAlg, 0); return false; }

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO ai;
    BCRYPT_INIT_AUTH_MODE_INFO(ai);
    ai.pbNonce = const_cast<PUCHAR>(nonce);
    ai.cbNonce = static_cast<ULONG>(kNonceSize);
    ai.pbTag   = tag;
    ai.cbTag   = static_cast<ULONG>(kTagSize);

    ULONG cipherSize = 0;
    NTSTATUS s = BCryptEncrypt(hKey,
        const_cast<PUCHAR>(plain.data()), static_cast<ULONG>(plain.size()),
        &ai, nullptr, 0, nullptr, 0, &cipherSize, 0);
    if (!NtOk(s)) { BCryptDestroyKey(hKey); BCryptCloseAlgorithmProvider(hAlg, 0); error = "BCryptEncrypt(size) failed: " + NtStr(s); return false; }

    cipher.resize(cipherSize);
    s = BCryptEncrypt(hKey,
        const_cast<PUCHAR>(plain.data()), static_cast<ULONG>(plain.size()),
        &ai, nullptr, 0, cipher.data(), cipherSize, &cipherSize, 0);
    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);
    if (!NtOk(s)) { error = "BCryptEncrypt(data) failed: " + NtStr(s); return false; }
    cipher.resize(cipherSize);
    return true;
}

bool Aes256GcmDecrypt(const std::array<uint8_t, kKeySize>& key,
                      const std::vector<uint8_t>& cipher,
                      const uint8_t nonce[kNonceSize],
                      const uint8_t tag[kTagSize],
                      std::vector<uint8_t>& plain,
                      std::string& error)
{
    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_KEY_HANDLE hKey = nullptr;
    std::vector<uint8_t> keyObj;

    if (!OpenAesGcm(hAlg, error)) return false;
    if (!BuildAesKey(hAlg, key, hKey, keyObj, error)) { BCryptCloseAlgorithmProvider(hAlg, 0); return false; }

    BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO ai;
    BCRYPT_INIT_AUTH_MODE_INFO(ai);
    ai.pbNonce = const_cast<PUCHAR>(nonce);
    ai.cbNonce = static_cast<ULONG>(kNonceSize);
    ai.pbTag   = const_cast<PUCHAR>(tag);
    ai.cbTag   = static_cast<ULONG>(kTagSize);

    ULONG plainSize = 0;
    NTSTATUS s = BCryptDecrypt(hKey,
        const_cast<PUCHAR>(cipher.data()), static_cast<ULONG>(cipher.size()),
        &ai, nullptr, 0, nullptr, 0, &plainSize, 0);
    if (!NtOk(s)) { BCryptDestroyKey(hKey); BCryptCloseAlgorithmProvider(hAlg, 0); error = "BCryptDecrypt(size) failed: " + NtStr(s); return false; }

    plain.resize(plainSize);
    s = BCryptDecrypt(hKey,
        const_cast<PUCHAR>(cipher.data()), static_cast<ULONG>(cipher.size()),
        &ai, nullptr, 0, plain.data(), plainSize, &plainSize, 0);
    BCryptDestroyKey(hKey);
    BCryptCloseAlgorithmProvider(hAlg, 0);
    if (!NtOk(s))
    {
        error = "BCryptDecrypt(data) failed: " + NtStr(s) +
                "\n  Probabile chiave sbagliata. Usa F) per ri-sincronizzare dal server.";
        return false;
    }
    plain.resize(plainSize);
    return true;
}

/* ── SAH detect ───────────────────────────────────────── */

bool IsPlainSah(const std::vector<uint8_t>& d)
{
    return d.size() >= 3 && d[0] == 'S' && d[1] == 'A' && d[2] == 'H';
}

bool IsEncryptedSah(const std::vector<uint8_t>& d)
{
    return d.size() >= sizeof(SahEncHeaderV1)
        && d[0] == 'E' && d[1] == 'S' && d[2] == 'A' && d[3] == 'H';
}

/* ── Encrypt / Decrypt buffers ────────────────────────── */

bool EncryptBuffer(const std::array<uint8_t, kKeySize>& key,
                   const std::vector<uint8_t>& plain,
                   std::vector<uint8_t>& encrypted,
                   std::string& error)
{
    SahEncHeaderV1 hdr{};
    std::memcpy(hdr.magic, "ESAH", 4);
    hdr.version   = kFormatVersion;
    hdr.flags     = kFlagAes256Gcm;
    hdr.plainSize = static_cast<uint64_t>(plain.size());

    if (!GenRandomBytes(hdr.nonce, kNonceSize, error)) return false;

    std::vector<uint8_t> cipher;
    if (!Aes256GcmEncrypt(key, plain, hdr.nonce, cipher, hdr.tag, error)) return false;

    encrypted.resize(sizeof(SahEncHeaderV1) + cipher.size());
    std::memcpy(encrypted.data(), &hdr, sizeof(SahEncHeaderV1));
    if (!cipher.empty())
        std::memcpy(encrypted.data() + sizeof(SahEncHeaderV1), cipher.data(), cipher.size());
    return true;
}

bool DecryptBuffer(const std::array<uint8_t, kKeySize>& key,
                   const std::vector<uint8_t>& encrypted,
                   std::vector<uint8_t>& plain,
                   std::string& error)
{
    if (encrypted.size() < sizeof(SahEncHeaderV1))
    {
        error = "File troppo piccolo per contenere header ESAH.";
        return false;
    }

    SahEncHeaderV1 hdr{};
    std::memcpy(&hdr, encrypted.data(), sizeof(SahEncHeaderV1));

    if (std::memcmp(hdr.magic, "ESAH", 4) != 0) { error = "Magic non valido. Non e' un file ESAH."; return false; }
    if (hdr.version != kFormatVersion) { error = "Versione non supportata: " + std::to_string(hdr.version); return false; }
    if (hdr.flags != kFlagAes256Gcm)  { error = "Flags non supportati: " + std::to_string(hdr.flags); return false; }

    const std::vector<uint8_t> cipher(
        encrypted.begin() + static_cast<std::ptrdiff_t>(sizeof(SahEncHeaderV1)),
        encrypted.end());

    return Aes256GcmDecrypt(key, cipher, hdr.nonce, hdr.tag, plain, error);
}

/* ── In-place file conversion ─────────────────────────── */

fs::path BuildBackupPath(const fs::path& p, Mode mode)
{
    const std::wstring suffix = (mode == Mode::Encrypt) ? L".dec" : L".enc";
    return p.parent_path() / fs::path(p.stem().wstring() + suffix + p.extension().wstring());
}

bool ReplaceFileInPlace(const fs::path& orig,
                        const std::vector<uint8_t>& newData,
                        const fs::path& backup,
                        std::string& error)
{
    const fs::path tmp = orig.parent_path() / fs::path(orig.filename().wstring() + L".tmp_sahcrypt");

    std::error_code ec;
    if (fs::exists(backup, ec) && !ec)
    {
        fs::remove(backup, ec);
        if (ec) { error = "Impossibile rimuovere backup: " + PathToStr(backup); return false; }
    }

    if (!WriteFileBytes(tmp, newData, error)) return false;

    try
    {
        fs::rename(orig, backup);
        fs::rename(tmp, orig);
    }
    catch (const std::exception& ex)
    {
        std::error_code ign;
        fs::remove(tmp, ign);
        if (!fs::exists(orig, ign) && fs::exists(backup, ign))
            fs::rename(backup, orig, ign);
        error = std::string("Sostituzione file fallita: ") + ex.what();
        return false;
    }
    return true;
}

bool RunInPlaceConversion(const fs::path& path,
                          Mode mode,
                          const std::array<uint8_t, kKeySize>& key,
                          std::string& error)
{
    std::vector<uint8_t> input;
    if (!ReadFileBytes(path, input, error)) return false;

    std::vector<uint8_t> output;
    if (mode == Mode::Encrypt)
    {
        if (!IsPlainSah(input)) { error = "Il file non sembra un SAH valido (magic SAH mancante)."; return false; }
        if (!EncryptBuffer(key, input, output, error)) return false;
    }
    else
    {
        if (!IsEncryptedSah(input)) { error = "Il file non sembra ESAH (magic ESAH mancante)."; return false; }
        if (!DecryptBuffer(key, input, output, error)) return false;
        if (!IsPlainSah(output)) { error = "Decrypt riuscito ma output non e' un SAH valido."; return false; }
    }

    const fs::path backup = BuildBackupPath(path, mode);
    if (!ReplaceFileInPlace(path, output, backup, error)) return false;

    std::cout << "\n[ok] Conversione completata.\n";
    std::cout << "     File attivo : " << PathToStr(path) << "\n";
    std::cout << "     Backup      : " << PathToStr(backup) << "\n";
    std::cout << "     Modalita'   : " << (mode == Mode::Encrypt ? "encrypt" : "decrypt") << "\n";
    return true;
}

/* ── UI ───────────────────────────────────────────────── */

bool SelectFileDialog(fs::path& selectedPath, std::string& error)
{
    wchar_t buf[MAX_PATH] = {};
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner   = GetConsoleWindow();
    ofn.lpstrFile   = buf;
    ofn.nMaxFile    = MAX_PATH;
    ofn.lpstrFilter = L"SAH Files (*.sah)\0*.sah\0All Files (*.*)\0*.*\0\0";
    ofn.lpstrTitle  = L"Seleziona Data.sah";
    ofn.Flags       = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

    if (!GetOpenFileNameW(&ofn))
    {
        const DWORD e = CommDlgExtendedError();
        if (e == 0) error = "Selezione annullata.";
        else
        {
            std::ostringstream o;
            o << "GetOpenFileNameW failed: 0x" << std::hex << std::uppercase << e;
            error = o.str();
        }
        return false;
    }

    selectedPath = fs::path(buf);
    return true;
}

enum class MenuAction { Encrypt, Decrypt, FetchKey, Exit };

MenuAction AskMenu(bool keyLoaded)
{
    while (true)
    {
        std::cout << "\nScegli operazione:\n";
        if (keyLoaded)
        {
            std::cout << "  1) Encrypt Data.sah\n";
            std::cout << "  2) Decrypt Data.sah\n";
            std::cout << "  F) Ri-sincronizza chiave dal server (SSH)\n";
        }
        else
        {
            std::cout << "  F) Scarica chiave dal server (SSH)\n";
        }
        std::cout << "  Q) Esci\n> ";

        std::string input;
        if (!std::getline(std::cin, input)) return MenuAction::Exit;
        if (input == "1" && keyLoaded)       return MenuAction::Encrypt;
        if (input == "2" && keyLoaded)       return MenuAction::Decrypt;
        if (input == "f" || input == "F")    return MenuAction::FetchKey;
        if (input == "q" || input == "Q")    return MenuAction::Exit;
        std::cout << "Scelta non valida. Riprova.\n";
    }
}

void PrintGuide()
{
    std::cout << "=========================================\n";
    std::cout << "      SAHCRYPT - Data.sah Tool\n";
    std::cout << "=========================================\n\n";
    std::cout << "La chiave viene cercata in questo ordine:\n";
    std::cout << "  1. SahKeyHex in sahcrypt.ini  (cache locale, accanto all'exe)\n";
    std::cout << "  2. SahKeyHex in patchmaker.ini (accanto all'exe)\n";
    std::cout << "  3. SSH fetch da GMSecurity.ini sul server\n";
    std::cout << "     Configura in sahcrypt.ini:\n";
    std::cout << "       SshHost=<ip>\n";
    std::cout << "       SshUser=<utente>\n";
    std::cout << "       SshRemotePath=<path GMSecurity.ini>\n";
    std::cout << "       SshKeyPath=<chiave privata>   ; oppure SshPassword=<pass>\n";
    std::cout << "       SahKeyName=SahCryptKeyHex     ; nome chiave (default)\n\n";
    std::cout << "Backup naming:\n";
    std::cout << "  Encrypt -> vecchio file diventa Data.dec.sah\n";
    std::cout << "  Decrypt -> vecchio file diventa Data.enc.sah\n\n";
    std::cout << "CLI: sahcrypt.exe encrypt|decrypt <path\\data.sah>\n\n";
}

/* ── Entry point ──────────────────────────────────────── */

int wmain(int argc, wchar_t* argv[])
{
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleTitleW(L"SahCrypt - Data.sah Encrypt/Decrypt Tool");

    PrintGuide();

    try
    {
        // CLI mode
        if (argc == 3)
        {
            const std::wstring modeArg = argv[1];
            const fs::path pathArg = fs::path(argv[2]);
            Mode mode{};

            if      (modeArg == L"encrypt") mode = Mode::Encrypt;
            else if (modeArg == L"decrypt") mode = Mode::Decrypt;
            else
            {
                std::cerr << "[err] Modalita' non valida. Usa: encrypt o decrypt\n";
                PauseAtEnd();
                return 1;
            }

            std::array<uint8_t, kKeySize> key{};
            std::string keyError;
            if (!LoadSahKey(key, keyError))
            {
                std::cerr << "\n[err] " << keyError << "\n";
                PauseAtEnd();
                return 1;
            }

            std::string error;
            if (!RunInPlaceConversion(pathArg, mode, key, error))
            {
                std::cerr << "[err] " << error << "\n";
                PauseAtEnd();
                return 2;
            }

            PauseAtEnd();
            return 0;
        }

        // Interactive mode
        std::array<uint8_t, kKeySize> key{};
        bool keyLoaded = false;

        {
            std::string keyError;
            keyLoaded = LoadSahKey(key, keyError);
            if (!keyLoaded)
                std::cout << "\n[warn] " << keyError << "\n";
        }

        while (true)
        {
            MenuAction action = AskMenu(keyLoaded);
            if (action == MenuAction::Exit) break;

            if (action == MenuAction::FetchKey)
            {
                std::string keyError;
                keyLoaded = LoadSahKey(key, keyError, /*forceRefresh=*/true);
                if (!keyLoaded)
                    std::cerr << "\n[err] " << keyError << "\n";
                continue;
            }

            Mode mode = (action == MenuAction::Encrypt) ? Mode::Encrypt : Mode::Decrypt;
            fs::path selectedPath;
            std::string error;

            if (!SelectFileDialog(selectedPath, error))
            {
                std::cerr << "[err] " << error << "\n";
                continue;
            }

            std::cout << "\nFile: " << PathToStr(selectedPath) << "\n";
            if (!RunInPlaceConversion(selectedPath, mode, key, error))
                std::cerr << "[err] " << error << "\n";
        }

        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "[err] Eccezione: " << ex.what() << "\n";
        PauseAtEnd();
        return 3;
    }
}
