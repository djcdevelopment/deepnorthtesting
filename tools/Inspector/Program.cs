using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static int _failures;

    private static readonly string[] CoreModdingTypes =
    [
        "Player", "Humanoid", "Character", "SEMan", "SE_Stats", "StatusEffect",
        "ZDOMan", "ZDO", "ZNet", "ZNetScene", "ZNetView", "ZRoutedRpc",
        "ZoneSystem", "Heightmap", "TerrainComp", "ObjectDB", "Inventory",
        "InventoryGui", "ItemDrop", "Hud", "Chat", "Terminal", "Game",
        "GameCamera", "PieceTable", "Piece", "WearNTear", "Container",
        "Minimap", "EnvMan", "AudioMan", "FejdStartup", "Achievements",
        "Skills", "Attack", "Projectile", "Tameable", "MonsterAI", "BaseAI",
        "Ship", "Fireplace", "Smelter", "Fermenter", "Beehive",
        "TeleportWorld", "TombStone", "VisEquipment", "Vector2s", "Vector2i"
    ];

    private static string ResolveDefaultGameDir()
    {
        string? envDir = Environment.GetEnvironmentVariable("VALHEIM_DIR");
        if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir))
        {
            return envDir;
        }
        if (Directory.Exists("/mnt/valheim/valheim_Data/Managed"))
        {
            return "/mnt/valheim";
        }
        string linuxDefault = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "Steam", "steamapps", "common", "Valheim");
        if (Directory.Exists(linuxDefault))
        {
            return linuxDefault;
        }
        return @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
    }

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "--query-symbol" or "--audit-plugin" or "--dump-index")
        {
            return RunExtendedMode(args);
        }

        Console.WriteLine("==========================================================");
        Console.WriteLine(" DeepNorthTesting :: Valheim Compatibility Inspector");
        Console.WriteLine("==========================================================");

        string gameDir = args.Length > 0 ? args[0] : ResolveDefaultGameDir();
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
            if (File.Exists(pluginDirectory))
            {
                AuditSinglePlugin(pluginDirectory, LoadGameModules(managedDir));
            }
            else
            {
                AuditPluginDirectory(pluginDirectory, LoadGameModules(managedDir));
            }
        }

        Console.WriteLine($"\n[Summary] {_failures} compatibility failure(s).");
        return _failures == 0 ? 0 : 1;
    }

    private static int RunExtendedMode(string[] args)
    {
        string mode = args[0];
        string gameDir = ResolveDefaultGameDir();
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--game-dir")
            {
                gameDir = args[i + 1];
            }
        }
        string managedDir = Path.Combine(gameDir, "valheim_Data", "Managed");
        List<ModuleDefinition> modules = LoadGameModules(managedDir);
        try
        {
            if (mode == "--query-symbol")
            {
                if (args.Length < 2)
                {
                    Fail("Usage: Inspector --query-symbol <TypeName>[.<MemberName>] [--out-cs <path>]");
                    return 1;
                }
                string query = args[1];
                string? outCs = null;
                for (int i = 2; i < args.Length - 1; i++)
                {
                    if (args[i] == "--out-cs")
                    {
                        outCs = args[i + 1];
                    }
                }
                string rendered = RenderSymbolStub(modules, query);
                if (string.IsNullOrEmpty(rendered))
                {
                    Fail($"Symbol not found in Valheim 1.0.21 assemblies: {query}");
                    return 1;
                }
                if (!string.IsNullOrEmpty(outCs))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outCs))!);
                    File.WriteAllText(outCs, rendered, new UTF8Encoding(false));
                }
                else
                {
                    Console.Write(rendered);
                }
                return 0;
            }

            if (mode == "--dump-index")
            {
                string outDir = args.Length > 1 && !args[1].StartsWith("--")
                    ? args[1]
                    : "/dev/shm/valheim-index";
                return DumpShmIndex(modules, managedDir, outDir);
            }

            if (mode == "--audit-plugin")
            {
                if (args.Length < 2)
                {
                    Fail("Usage: Inspector --audit-plugin <path.dll>");
                    return 1;
                }
                string targetPath = args[1];
                if (File.Exists(targetPath))
                {
                    AuditSinglePlugin(targetPath, modules);
                }
                else if (Directory.Exists(targetPath))
                {
                    AuditPluginDirectory(targetPath, modules);
                }
                else
                {
                    Fail($"Target not found: {targetPath}");
                }
                Console.WriteLine($"\n[Summary] {_failures} compatibility failure(s).");
                return _failures == 0 ? 0 : 1;
            }

            return 1;
        }
        finally
        {
            foreach (ModuleDefinition m in modules)
            {
                m.Dispose();
            }
        }
    }

    private static List<ModuleDefinition> LoadGameModules(string managedDir)
    {
        string[] dllNames = ["assembly_valheim.dll", "assembly_utils.dll", "assembly_guiutils.dll"];
        List<ModuleDefinition> list = [];
        foreach (string name in dllNames)
        {
            string p = Path.Combine(managedDir, name);
            if (File.Exists(p))
            {
                list.Add(ModuleDefinition.ReadModule(p));
            }
        }
        return list;
    }

    private static TypeDefinition? FindGameType(IEnumerable<ModuleDefinition> modules, string typeName)
    {
        foreach (ModuleDefinition mod in modules)
        {
            TypeDefinition? direct = mod.GetType(typeName);
            if (direct is not null)
            {
                return direct;
            }
            foreach (TypeDefinition t in mod.Types)
            {
                if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
                foreach (TypeDefinition nested in t.NestedTypes)
                {
                    if (string.Equals(nested.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals($"{t.Name}.{nested.Name}", typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return nested;
                    }
                }
            }
        }
        return null;
    }

    private static string FormatTypeName(TypeReference tr)
    {
        if (tr is ByReferenceType byRef)
        {
            return $"ref {FormatTypeName(byRef.ElementType)}";
        }
        if (tr is ArrayType arr)
        {
            return $"{FormatTypeName(arr.ElementType)}[]";
        }
        if (tr is GenericInstanceType git)
        {
            string baseName = git.ElementType.Name.Split('`')[0];
            string args = string.Join(", ", git.GenericArguments.Select(FormatTypeName));
            return $"{baseName}<{args}>";
        }
        return tr.Name switch
        {
            "Void" => "void",
            "Boolean" => "bool",
            "Int32" => "int",
            "Int16" => "short",
            "Int64" => "long",
            "Single" => "float",
            "Double" => "double",
            "String" => "string",
            "Object" => "object",
            "Byte" => "byte",
            _ => tr.Name
        };
    }

    private static string RenderSymbolStub(List<ModuleDefinition> modules, string query)
    {
        string typePart = query;
        string? memberFilter = null;
        if (query.Contains('.'))
        {
            TypeDefinition? fullMatch = FindGameType(modules, query);
            if (fullMatch is null)
            {
                int idx = query.LastIndexOf('.');
                typePart = query[..idx];
                memberFilter = query[(idx + 1)..];
            }
        }

        TypeDefinition? type = FindGameType(modules, typePart);
        if (type is null)
        {
            return string.Empty;
        }

        return RenderTypeToCSharp(type, memberFilter);
    }

    private static string RenderTypeToCSharp(TypeDefinition type, string? memberFilter = null)
    {
        StringBuilder sb = new();
        sb.AppendLine($"// Valheim 1.0.21 Ground-Truth Bytecode Stub ({type.Module.Name})");
        sb.AppendLine($"// Type: {type.FullName} (Base: {type.BaseType?.Name ?? "none"})");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();

        string kind = type.IsValueType ? (type.IsEnum ? "enum" : "struct") : (type.IsInterface ? "interface" : "class");
        string vis = type.IsPublic || type.IsNestedPublic ? "public" : "internal";
        string staticMod = type.IsAbstract && type.IsSealed ? " static" : "";
        sb.AppendLine($"{vis}{staticMod} {kind} {type.Name}");
        sb.AppendLine("{");

        foreach (FieldDefinition f in type.Fields)
        {
            if (memberFilter is not null && !f.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string fVis = f.IsPublic ? "public" : (f.IsFamily ? "protected" : "private");
            string fStat = f.IsStatic ? " static" : "";
            sb.AppendLine($"    {fVis}{fStat} {FormatTypeName(f.FieldType)} {f.Name};");
        }

        if (type.Fields.Count > 0)
        {
            sb.AppendLine();
        }

        foreach (TypeDefinition nested in type.NestedTypes)
        {
            if (memberFilter is not null &&
                !nested.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase) &&
                !nested.Fields.Any(nf => nf.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) &&
                !nested.Methods.Any(nm => nm.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            string nKind = nested.IsEnum ? "enum" : (nested.IsValueType ? "struct" : "class");
            sb.AppendLine($"    public {nKind} {nested.Name}");
            sb.AppendLine("    {");
            foreach (FieldDefinition nf in nested.Fields.Take(40))
            {
                sb.AppendLine($"        public {FormatTypeName(nf.FieldType)} {nf.Name};");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (MethodDefinition m in type.Methods)
        {
            if (m.IsConstructor && m.Name == ".cctor")
            {
                continue;
            }
            if (memberFilter is not null && !m.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string mVis = m.IsPublic ? "public" : (m.IsFamily ? "protected" : "private");
            string mStat = m.IsStatic ? " static" : "";
            string retType = m.IsConstructor ? "" : $"{FormatTypeName(m.ReturnType)} ";
            string mName = m.IsConstructor ? type.Name : m.Name.Replace("<", "_").Replace(">", "_").Replace("|", "_");
            string parms = string.Join(", ", m.Parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

            if (m.Name != mName)
            {
                sb.AppendLine($"    // Compiler-Generated Exact Method Name: \"{m.Name}\"");
            }
            sb.AppendLine($"    {mVis}{mStat} {retType}{mName}({parms})");
            sb.AppendLine("    {");

            if (m.HasBody)
            {
                if (m.Body.Variables.Count > 0)
                {
                    string locals = string.Join(", ", m.Body.Variables.Take(16).Select(v => $"V_{v.Index}:{FormatTypeName(v.VariableType)}"));
                    sb.AppendLine($"        // IL Locals ({m.Body.Variables.Count}): {locals}");
                }

                List<string> callsAndFields = [];
                foreach (Instruction ins in m.Body.Instructions)
                {
                    if (ins.Operand is MethodReference mr && !mr.Name.StartsWith("get_") && !mr.Name.StartsWith("set_"))
                    {
                        string callSig = $"{mr.DeclaringType.Name}.{mr.Name}({string.Join(",", mr.Parameters.Select(p => FormatTypeName(p.ParameterType)))})";
                        if (!callsAndFields.Contains(callSig))
                        {
                            callsAndFields.Add(callSig);
                        }
                    }
                    else if (ins.Operand is FieldReference fr)
                    {
                        string fldSig = $"{fr.DeclaringType.Name}.{fr.Name}";
                        if (!callsAndFields.Contains(fldSig))
                        {
                            callsAndFields.Add(fldSig);
                        }
                    }
                    if (callsAndFields.Count >= 18)
                    {
                        break;
                    }
                }
                if (callsAndFields.Count > 0)
                {
                    sb.AppendLine($"        // IL References: {string.Join(" -> ", callsAndFields)}");
                }
            }
            if (!m.IsConstructor && m.ReturnType.MetadataType != MetadataType.Void)
            {
                sb.AppendLine("        return default;");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static int DumpShmIndex(List<ModuleDefinition> modules, string managedDir, string outDir)
    {
        string classesDir = Path.Combine(outDir, "classes");
        Directory.CreateDirectory(classesDir);

        ModuleDefinition valheimMod = modules[0];
        string valheimDll = Path.Combine(managedDir, "assembly_valheim.dll");
        string version = ReadGameVersion(valheimMod);
        string sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(valheimDll)));

        Dictionary<string, object> catalogTypes = new(StringComparer.OrdinalIgnoreCase);
        int dumpedFiles = 0;

        foreach (string tName in CoreModdingTypes)
        {
            TypeDefinition? td = FindGameType(modules, tName);
            if (td is null)
            {
                continue;
            }
            string csText = RenderTypeToCSharp(td);
            string csPath = Path.Combine(classesDir, $"{td.Name}.cs");
            File.WriteAllText(csPath, csText, new UTF8Encoding(false));
            dumpedFiles++;

            catalogTypes[td.Name] = new
            {
                module = td.Module.Name,
                cs_file = csPath,
                fields = td.Fields.Select(f => $"{FormatTypeName(f.FieldType)} {f.Name}").ToArray(),
                methods = td.Methods
                    .Where(m => !m.IsConstructor)
                    .Select(m => $"{FormatTypeName(m.ReturnType)} {m.Name}({string.Join(", ", m.Parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"))})")
                    .ToArray(),
            };
        }

        var catalog = new
        {
            schema = "valheim-1.0.21-bytecode-index.v1",
            game_version = version,
            assembly_valheim_sha256 = sha256,
            classes_dumped = dumpedFiles,
            classes_dir = classesDir,
            types = catalogTypes
        };

        string catalogPath = Path.Combine(outDir, "catalog.json");
        File.WriteAllText(
            catalogPath,
            JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        Console.WriteLine($"[+] Dumped {dumpedFiles} Valheim {version} C# bytecode stubs to {classesDir}");
        Console.WriteLine($"[+] Catalog written to {catalogPath}");
        return 0;
    }

    private static void AuditSinglePlugin(string pluginPath, List<ModuleDefinition> gameModules)
    {
        string gameVersion = ReadGameVersion(gameModules.Single(m => m.Name == "assembly_valheim.dll"));
        Console.WriteLine($"\n[+] Auditing plugin assembly: {pluginPath}");
        try
        {
            using ModuleDefinition plugin = ModuleDefinition.ReadModule(pluginPath);

            // 1. Check HarmonyPatch attributes on all types and methods (including split class Type + method Name)
            int harmonyPatchesChecked = 0;
            foreach (TypeDefinition type in plugin.GetTypes())
            {
                TypeReference? inheritedTarget = ExtractHarmonyTargetType(type.CustomAttributes);
                VerifyHarmonyAttributes(type.CustomAttributes, type.FullName, null, gameModules, ref harmonyPatchesChecked);
                foreach (MethodDefinition method in type.Methods)
                {
                    VerifyHarmonyAttributes(method.CustomAttributes, $"{type.Name}.{method.Name}", inheritedTarget, gameModules, ref harmonyPatchesChecked);
                }
            }

            // 2. Check all MethodReference & FieldReference targets pointing into assembly_valheim / assembly_utils
            HashSet<string> gameAsmNames = ["assembly_valheim", "assembly_utils", "assembly_guiutils"];
            int memberRefsChecked = 0;
            foreach (MemberReference memberRef in plugin.GetMemberReferences())
            {
                string? scopeName = memberRef.DeclaringType.Scope?.Name;
                if (scopeName is null)
                {
                    continue;
                }
                string cleanScope = scopeName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? scopeName[..^4]
                    : scopeName;
                if (!gameAsmNames.Contains(cleanScope))
                {
                    continue;
                }

                TypeDefinition? targetType = FindGameType(gameModules, memberRef.DeclaringType.Name);
                if (targetType is null)
                {
                    Fail($"{Path.GetFileName(pluginPath)}: referenced game type '{memberRef.DeclaringType.FullName}' does not exist in Valheim {gameVersion}");
                    continue;
                }

                if (memberRef is MethodReference mRef)
                {
                    bool found = targetType.Methods.Any(m =>
                        m.Name == mRef.Name &&
                        m.Parameters.Count == mRef.Parameters.Count &&
                        m.Parameters.Zip(mRef.Parameters, (a, b) => a.ParameterType.Name == b.ParameterType.Name).All(x => x));
                    if (!found)
                    {
                        Fail($"{Path.GetFileName(pluginPath)}: referenced method '{targetType.Name}.{mRef.Name}({string.Join(", ", mRef.Parameters.Select(p => p.ParameterType.Name))})' missing or signature changed in Valheim {gameVersion}");
                    }
                    else
                    {
                        memberRefsChecked++;
                    }
                }
                else if (memberRef is FieldReference fRef)
                {
                    bool found = targetType.Fields.Any(f => f.Name == fRef.Name);
                    if (!found)
                    {
                        Fail($"{Path.GetFileName(pluginPath)}: referenced field '{targetType.Name}.{fRef.Name}' missing in Valheim {gameVersion}");
                    }
                    else
                    {
                        memberRefsChecked++;
                    }
                }
            }

            Pass($"{Path.GetFileName(pluginPath)}: verified {harmonyPatchesChecked} [HarmonyPatch] target(s) and {memberRefsChecked} game member reference(s) against Valheim {gameVersion}");
        }
        catch (BadImageFormatException)
        {
            Console.WriteLine($"  [SKIP] {Path.GetFileName(pluginPath)} is not a managed assembly");
        }
    }

    private static TypeReference? ExtractHarmonyTargetType(Mono.Collections.Generic.Collection<CustomAttribute> attrs)
    {
        foreach (CustomAttribute attr in attrs)
        {
            if (attr.AttributeType.Name != "HarmonyPatch")
            {
                continue;
            }
            foreach (CustomAttributeArgument arg in attr.ConstructorArguments)
            {
                if (arg.Type.Name == "Type" && arg.Value is TypeReference tr)
                {
                    return tr;
                }
            }
        }
        return null;
    }

    private static void VerifyHarmonyAttributes(
        Mono.Collections.Generic.Collection<CustomAttribute> attrs,
        string ownerLabel,
        TypeReference? inheritedTarget,
        List<ModuleDefinition> gameModules,
        ref int checkedCount)
    {
        TypeReference? classLevelTarget = inheritedTarget;
        string? classLevelMethod = null;
        TypeReference[]? classLevelArgTypes = null;

        foreach (CustomAttribute attr in attrs)
        {
            if (attr.AttributeType.Name != "HarmonyPatch")
            {
                continue;
            }
            foreach (CustomAttributeArgument arg in attr.ConstructorArguments)
            {
                if (arg.Type.Name == "Type" && arg.Value is TypeReference tr)
                {
                    classLevelTarget = tr;
                }
                else if (arg.Type.MetadataType == MetadataType.String && arg.Value is string s)
                {
                    classLevelMethod = s;
                }
                else if (arg.Value is CustomAttributeArgument[] arr)
                {
                    classLevelArgTypes = arr.Select(a => a.Value).OfType<TypeReference>().ToArray();
                }
            }
        }

        if (classLevelTarget is not null && !string.IsNullOrEmpty(classLevelMethod))
        {
            checkedCount++;
            TypeDefinition? gameType = FindGameType(gameModules, classLevelTarget.Name);
            if (gameType is null)
            {
                Fail($"[HarmonyPatch] on {ownerLabel}: target type '{classLevelTarget.FullName}' not found in Valheim 1.0.21");
                return;
            }
            var candidates = gameType.Methods.Where(m => m.Name == classLevelMethod).ToList();
            if (candidates.Count == 0)
            {
                Fail($"[HarmonyPatch] on {ownerLabel}: target method '{gameType.Name}.{classLevelMethod}' not found in Valheim 1.0.21");
                return;
            }
            if (classLevelArgTypes is not null)
            {
                bool exactOverload = candidates.Any(m =>
                    m.Parameters.Count == classLevelArgTypes.Length &&
                    m.Parameters.Zip(classLevelArgTypes, (p, t) => p.ParameterType.Name.TrimEnd('&') == t.Name.TrimEnd('&')).All(x => x));
                if (!exactOverload)
                {
                    string actualSigs = string.Join(" | ", candidates.Select(c => $"({string.Join(", ", c.Parameters.Select(p => FormatTypeName(p.ParameterType)))})"));
                    Fail($"[HarmonyPatch] on {ownerLabel}: overload '{gameType.Name}.{classLevelMethod}({string.Join(", ", classLevelArgTypes.Select(t => t.Name))})' does not match Valheim 1.0.21 signatures: {actualSigs}");
                    return;
                }
            }
            else if (candidates.Count > 1)
            {
                // Warn/Fail on ambiguous overloads unless all candidates have identical parameter counts or are disambiguated on methods
                string actualSigs = string.Join(" | ", candidates.Select(c => $"({string.Join(", ", c.Parameters.Select(p => FormatTypeName(p.ParameterType)))})"));
                Fail($"[HarmonyPatch] on {ownerLabel}: '{gameType.Name}.{classLevelMethod}' has {candidates.Count} overloads in Valheim 1.0.21 ({actualSigs}) — disambiguate with typeof(...) parameter array to avoid AmbiguousMatchException!");
                return;
            }
            Pass($"[HarmonyPatch] {ownerLabel} -> {gameType.Name}.{classLevelMethod}");
        }
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

    private static void AuditPluginDirectory(string pluginDirectory, List<ModuleDefinition> gameModules)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            Fail($"Plugin audit directory not found: {pluginDirectory}");
            return;
        }
        Console.WriteLine($"\n[+] Auditing candidate assemblies in {pluginDirectory}");
        foreach (string pluginPath in Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories))
        {
            AuditSinglePlugin(pluginPath, gameModules);
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
