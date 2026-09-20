using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static int _failures;

    private static int Main(string[] args)
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine(" DeepNorthTesting :: Valheim Compatibility Inspector");
        Console.WriteLine("==========================================================");

        string gameDir = args.Length > 0
            ? args[0]
            : @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
        string? expectedVersion = args.Length > 1 ? args[1] : null;
        string? pluginDirectory = args.Length > 2 ? args[2] : null;
        string managedDir = Path.Combine(gameDir, "valheim_Data", "Managed");
        string valheimDll = Path.Combine(managedDir, "assembly_valheim.dll");

        if (!File.Exists(valheimDll))
        {
            Fail($"Game assembly not found: {valheimDll}");
            return 1;
        }

        using ModuleDefinition module = ModuleDefinition.ReadModule(valheimDll);
        string detectedVersion = ReadGameVersion(module);
        string assemblyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(valheimDll)));
        Console.WriteLine($"[+] Valheim directory : {gameDir}");
        Console.WriteLine($"[+] Valheim version   : {detectedVersion}");
        Console.WriteLine($"[+] Assembly SHA-256  : {assemblyHash}");
        Console.WriteLine($"EVIDENCE game_version={detectedVersion} assembly_sha256={assemblyHash}");

        if (!string.IsNullOrWhiteSpace(expectedVersion) && detectedVersion != expectedVersion)
        {
            Fail($"Expected Valheim {expectedVersion}, found {detectedVersion}");
        }

        Console.WriteLine("\n[+] Auditing fleet hook and reflection targets");

        CheckField(module, "Game", "isModded", "IsModded");
        CheckMethod(module, "Achievements", "IsCheatedAtAll", m => m.Parameters.Count == 0 && m.ReturnType.MetadataType == MetadataType.Boolean, "IsModded");
        CheckMethod(module, "FejdStartup", "SetupGui", _ => true, "IsModded");
        CheckMethod(module, "InventoryGui", "UpdateAchievementsList", _ => true, "IsModded");
        CheckMethod(module, "Terminal", "InitTerminal", _ => true, "IsModded/Unswayed");
        CheckIsModdedReference(module);

        CheckMethod(module, "Hud", "UpdateBlackScreen", _ => true, "Unfaded");
        CheckMethod(module, "Hud", "GetFadeDuration", _ => true, "Unfaded");
        CheckMethod(module, "Hud", "Update", _ => true, "Unfaded");
        CheckMethod(module, "Game", "RequestRespawn", _ => true, "Unfaded");
        CheckMethod(module, "Player", "OnDeath", _ => true, "Unfaded");
        CheckMethod(module, "GameCamera", "UpdateCamera", _ => true, "Unfaded");
        CheckMethod(module, "Terminal", "Awake", _ => true, "Unfaded");

        CheckMethod(module, "GameCamera", "GetCameraBaseOffset", _ => true, "Unswayed");
        CheckMethod(module, "GameCamera", "GetCameraPosition", _ => true, "Unswayed");
        CheckMethod(module, "GameCamera", "UpdateFOV", _ => true, "Unswayed");
        CheckMethod(module, "GameCamera", "UpdateCameraShake", _ => true, "Unswayed");

        CheckMethod(module, "Humanoid", "Pickup", m =>
            m.Parameters.Count == 3 &&
            m.Parameters[0].ParameterType.Name == "GameObject" &&
            m.Parameters[1].ParameterType.MetadataType == MetadataType.Boolean &&
            m.Parameters[2].ParameterType.MetadataType == MetadataType.Boolean,
            "TotemSentinel");
        CheckMethod(module, "Character", "ApplyDamage", _ => true, "TotemSentinel");
        CheckField(module, "ZDOMan", "m_objectsBySector", "TotemSentinel");
        CheckField(module, "ZDOMan", "m_width", "TotemSentinel");
        CheckMethod(module, "ZoneSystem", "SectorToIndex", m =>
            m.IsStatic && m.Parameters.Count == 2 &&
            m.Parameters.All(p => p.ParameterType.MetadataType == MetadataType.Int32),
            "TotemSentinel");

        CheckField(module, "Game", "m_hasStartedOnce", "SelfieStick");
        CheckMethod(module, "Game", "InIntro", _ => true, "SelfieStick");
        CheckMethod(module, "Game", "SkipIntro", _ => true, "SelfieStick");
        CheckMethod(module, "VisEquipment", "SetupCloth", _ => true, "SelfieStick");
        CheckMethod(module, "Fireplace", "UpdateState", _ => true, "SelfieStick");
        CheckField(module, "EnvMan", "m_environments", "SelfieStick");
        CheckField(module, "Hud", "m_userHidden", "SelfieStick");

        CheckMethod(module, "ZDOMan", "FindSectorObjects", m =>
            m.Parameters.Count >= 2 && m.Parameters[0].ParameterType.Name == "Vector2s",
            "Valheim 1.0 baseline");
        CheckMethod(module, "Character", "Message", m =>
            m.Parameters.Count == 5 && m.Parameters[4].ParameterType.MetadataType == MetadataType.Boolean,
            "Valheim 1.0 baseline");

        if (!string.IsNullOrWhiteSpace(pluginDirectory))
        {
            AuditPluginDirectory(pluginDirectory);
        }

        Console.WriteLine($"\n[Summary] {_failures} compatibility failure(s).");
        return _failures == 0 ? 0 : 1;
    }

    private static string ReadGameVersion(ModuleDefinition module)
    {
        TypeDefinition? versionType = module.GetType("Version");
        MethodDefinition? initializer = versionType?.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.HasBody);
        if (initializer is null)
        {
            Fail("Version static initializer was not found");
            return "unknown";
        }

        int[] values = initializer.Body.Instructions
            .Select(ReadInt32Constant)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Take(3)
            .ToArray();
        if (values.Length != 3)
        {
            Fail("Could not decode Valheim semantic version from Version..cctor");
            return "unknown";
        }
        return string.Join('.', values);
    }

    private static int? ReadInt32Constant(Instruction instruction) => instruction.OpCode.Code switch
    {
        Code.Ldc_I4_M1 => -1,
        Code.Ldc_I4_0 => 0,
        Code.Ldc_I4_1 => 1,
        Code.Ldc_I4_2 => 2,
        Code.Ldc_I4_3 => 3,
        Code.Ldc_I4_4 => 4,
        Code.Ldc_I4_5 => 5,
        Code.Ldc_I4_6 => 6,
        Code.Ldc_I4_7 => 7,
        Code.Ldc_I4_8 => 8,
        Code.Ldc_I4_S => Convert.ToInt32(instruction.Operand),
        Code.Ldc_I4 => Convert.ToInt32(instruction.Operand),
        _ => null
    };

    private static void CheckMethod(
        ModuleDefinition module,
        string typeName,
        string methodName,
        Func<MethodDefinition, bool> validator,
        string owner)
    {
        TypeDefinition? type = module.GetType(typeName);
        MethodDefinition? method = type?.Methods.FirstOrDefault(m => m.Name == methodName && validator(m));
        if (method is null)
        {
            Fail($"{typeName}.{methodName} missing or changed (required by {owner})");
            return;
        }
        Pass($"{typeName}.{methodName} ({owner})");
    }

    private static void CheckField(ModuleDefinition module, string typeName, string fieldName, string owner)
    {
        TypeDefinition? type = module.GetType(typeName);
        if (type?.Fields.Any(f => f.Name == fieldName) != true)
        {
            Fail($"{typeName}.{fieldName} missing or changed (required by {owner})");
            return;
        }
        Pass($"{typeName}.{fieldName} ({owner})");
    }

    private static void CheckIsModdedReference(ModuleDefinition module)
    {
        MethodDefinition? method = module.GetType("Achievements")?.Methods
            .FirstOrDefault(m => m.Name == "IsCheatedAtAll" && m.HasBody);
        bool found = method?.Body.Instructions.Any(i =>
            i.Operand is FieldReference field && field.DeclaringType.Name == "Game" && field.Name == "isModded") == true;
        if (!found)
        {
            Fail("Achievements.IsCheatedAtAll no longer reads Game.isModded; review IsModded behavior and documentation");
            return;
        }
        Pass("Achievements.IsCheatedAtAll -> Game.isModded IL reference (IsModded)");
    }

    private static void AuditPluginDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            Fail($"Plugin audit directory not found: {pluginDirectory}");
            return;
        }
        Console.WriteLine($"\n[+] Auditing candidate assemblies in {pluginDirectory}");
        foreach (string pluginPath in Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                using ModuleDefinition plugin = ModuleDefinition.ReadModule(pluginPath);
                bool obsoleteCharacterMessage = plugin.GetMemberReferences().OfType<MethodReference>().Any(reference =>
                    reference.DeclaringType.Name == "Character" &&
                    reference.Name == "Message" &&
                    reference.Parameters.Count == 4);
                if (obsoleteCharacterMessage)
                {
                    Fail($"{Path.GetFileName(pluginPath)} references obsolete Character.Message(...4 parameters)");
                }
                else
                {
                    Pass($"{Path.GetFileName(pluginPath)} member references");
                }
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine($"  [SKIP] {Path.GetFileName(pluginPath)} is not a managed assembly");
            }
        }
    }

    private static void Pass(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [PASS] {message}");
        Console.ResetColor();
    }

    private static void Fail(string message)
    {
        _failures++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAIL] {message}");
        Console.ResetColor();
    }
}
