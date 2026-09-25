using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyRemap.Core;

/// <summary>
/// config.json lives next to the executable. Writing goes through a temp file plus a
/// replace so a crash mid-save cannot leave a truncated config (§4, §11).
/// </summary>
public static class ConfigStore
{
    public const string FileName = "config.json";
    public const string BackupName = "config.json.bak";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Portable root: the folder holding the exe, never AppData (§11).</summary>
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string FilePath => Path.Combine(BaseDirectory, FileName);

    public static string BackupPath => Path.Combine(BaseDirectory, BackupName);

    /// <summary>Raised with a human-readable message when the existing file had to be discarded.</summary>
    public static string? LastLoadWarning { get; private set; }

    public static AppConfig Load()
    {
        LastLoadWarning = null;
        var path = FilePath;

        if (!File.Exists(path)) return new AppConfig();

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var dto = JsonSerializer.Deserialize<ConfigDto>(json, Options);
            if (dto is null) throw new JsonException("配置内容为空");
            return FromDto(dto);
        }
        catch (Exception ex)
        {
            TryBackupCorrupt(path);
            LastLoadWarning = $"配置文件已损坏，已备份为 {BackupName} 并重建默认配置。\n({ex.Message})";
            return new AppConfig();
        }
    }

    /// <summary>Atomic save: temp file in the same directory, then replace.</summary>
    public static void Save(AppConfig config)
    {
        var path = FilePath;
        var temp = path + ".tmp";

        try
        {
            var json = JsonSerializer.Serialize(ToDto(config), Options);
            File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, destinationBackupFileName: null);
                }
                catch (IOException)
                {
                    // Some filesystems refuse Replace; fall back to an overwriting move.
                    File.Move(temp, path, overwrite: true);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch (Exception ex)
        {
            LastLoadWarning = $"配置保存失败：{ex.Message}";
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // Nothing useful to do; the original file is untouched.
            }
        }
    }

    private static void TryBackupCorrupt(string path)
    {
        try
        {
            File.Copy(path, BackupPath, overwrite: true);
        }
        catch
        {
            // Backup is best-effort; loading defaults still proceeds.
        }
    }

    private static AppConfig FromDto(ConfigDto dto)
    {
        var config = new AppConfig
        {
            CoverageEnabled = dto.CoverageEnabled,
            CloseBehavior = (CloseBehavior)dto.CloseBehavior,
            Theme = new AppTheme
            {
                Background = dto.Theme?.Background ?? AppTheme.DefaultBackground,
                Border = dto.Theme?.Border ?? AppTheme.DefaultBorder,
                Accent = dto.Theme?.Accent ?? AppTheme.DefaultAccent,
                Text = dto.Theme?.Text,
                Appearance = (AppearanceMode)(dto.Theme?.Appearance ?? 0),
            },
        };

        // 缺失或为空都视为默认快捷键；显式保存为 null 才表示用户关闭了快捷键。
        config.CoverageToggleHotkey = dto.CoverageToggleHotkey is { Length: > 0 } hk
            ? new KeyCombo(hk)
            : dto.HotkeyDisabled ? null : Hotkey.Default;

        foreach (var p in dto.Profiles ?? new List<ProfileDto>())
        {
            var profile = new Profile
            {
                Id = string.IsNullOrWhiteSpace(p.Id) ? Guid.NewGuid().ToString("N") : p.Id,
                Name = p.Name ?? "",
                Enabled = p.Enabled,
            };

            foreach (var r in p.Rules ?? new List<RuleDto>())
            {
                profile.Rules.Add(new Rule
                {
                    Id = string.IsNullOrWhiteSpace(r.Id) ? Guid.NewGuid().ToString("N") : r.Id,
                    Enabled = r.Enabled,
                    Source = r.Source is { Length: > 0 } s ? new KeyCombo(s) : null,
                    Target = r.Target is { Length: > 0 } t ? new KeyCombo(t) : null,
                });
            }

            config.Profiles.Add(profile);
        }

        config.RevalidateAll();
        return config;
    }

    private static ConfigDto ToDto(AppConfig config) => new()
    {
        CoverageEnabled = config.CoverageEnabled,
        CoverageToggleHotkey = config.CoverageToggleHotkey?.Keys,
        HotkeyDisabled = config.CoverageToggleHotkey is null,
        CloseBehavior = (int)config.CloseBehavior,
        Theme = new ThemeDto
        {
            Background = config.Theme.Background,
            Border = config.Theme.Border,
            Accent = config.Theme.Accent,
            Text = config.Theme.Text,
            Appearance = (int)config.Theme.Appearance,
        },
        Profiles = config.Profiles.Select(p => new ProfileDto
        {
            Id = p.Id,
            Name = p.Name,
            Enabled = p.Enabled,
            Rules = p.Rules.Select(r => new RuleDto
            {
                Id = r.Id,
                Enabled = r.Enabled,
                Source = r.Source?.Keys,
                Target = r.Target?.Keys,
            }).ToList(),
        }).ToList(),
    };

    private sealed class ConfigDto
    {
        public bool CoverageEnabled { get; set; } = true;
        public int[]? CoverageToggleHotkey { get; set; }
        public bool HotkeyDisabled { get; set; }
        public int CloseBehavior { get; set; }
        public ThemeDto? Theme { get; set; }
        public List<ProfileDto>? Profiles { get; set; }
    }

    private sealed class ThemeDto
    {
        public string? Background { get; set; }
        public string? Border { get; set; }
        public string? Accent { get; set; }
        public string? Text { get; set; }
        public int Appearance { get; set; }
    }

    private sealed class ProfileDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool Enabled { get; set; }
        public List<RuleDto>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        public string? Id { get; set; }
        public bool Enabled { get; set; }
        public int[]? Source { get; set; }
        public int[]? Target { get; set; }
    }
}
