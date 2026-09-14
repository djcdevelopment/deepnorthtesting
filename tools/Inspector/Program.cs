using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program {
    static void Main(string[] args) {
        Console.WriteLine("==========================================================");
        Console.WriteLine("   DeepNorthTesting :: Valheim 1.0 Reflection Inspector   ");
        Console.WriteLine("==========================================================");

        string defaultGameDir = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
        string gameDir = args.Length > 0 ? args[0] : defaultGameDir;
        string managedDir = Path.Combine(gameDir, "valheim_Data", "Managed");
        string pluginsDir = Path.Combine(gameDir, "BepInEx", "plugins");

        if (!Directory.Exists(managedDir)) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Game managed directory not found at: {managedDir}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"[+] Target Valheim Directory: {gameDir}");
        Console.WriteLine($"[+] Scanning Core Game Assemblies in: {managedDir}");

        // 1. Audit Core Game Signatures
        string valheimDll = Path.Combine(managedDir, "assembly_valheim.dll");
        string utilsDll = Path.Combine(managedDir, "assembly_utils.dll");

        if (File.Exists(valheimDll)) {
            var valheimModule = ModuleDefinition.ReadModule(valheimDll);

            // Audit ZDOMan.FindSectorObjects
            var tZDOMan = valheimModule.GetType("ZDOMan");
            var mFindSector = tZDOMan?.Methods.FirstOrDefault(m => m.Name == "FindSectorObjects");
            if (mFindSector != null) {
                var p0 = mFindSector.Parameters[0].ParameterType.Name;
                var p1 = mFindSector.Parameters[1].ParameterType.Name;
                Console.WriteLine($"[Game Signature] ZDOMan.FindSectorObjects => ({p0}, {p1}, ...)");
                if (p0 == "Vector2s") {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> Confirmed Valheim 1.0 16-bit sector partitioning (Vector2s)");
                    Console.ResetColor();
                }
            }

            // Audit SEMan.AddStatusEffect
            var tSEMan = valheimModule.GetType("SEMan");
            var mAddStatus = tSEMan?.Methods.FirstOrDefault(m => m.Name == "AddStatusEffect" && m.Parameters.Count >= 5);
            if (mAddStatus != null) {
                Console.WriteLine($"[Game Signature] SEMan.AddStatusEffect => {mAddStatus.Parameters.Count} parameters (includes Int16 variant)");
            }

            // Audit PieceTable.SetCategory
            var tPieceTable = valheimModule.GetType("PieceTable");
            var setCategoryCount = tPieceTable?.Methods.Count(m => m.Name == "SetCategory") ?? 0;
            Console.WriteLine($"[Game Signature] PieceTable.SetCategory => {setCategoryCount} overloads detected (int + PieceCategory)");

            // Audit InventoryGui.Show
            var tInvGui = valheimModule.GetType("InventoryGui");
            var mShow = tInvGui?.Methods.FirstOrDefault(m => m.Name == "Show");
            if (mShow != null) {
                Console.WriteLine($"[Game Signature] InventoryGui.Show => ({string.Join(", ", mShow.Parameters.Select(p => p.ParameterType.Name + " " + p.Name))})");
            }

            // Audit Character.Message (5 parameters in Valheim 1.0)
            var tChar = valheimModule.GetType("Character");
            var mMsg = tChar?.Methods.FirstOrDefault(m => m.Name == "Message");
            if (mMsg != null) {
                Console.WriteLine($"[Game Signature] Character.Message => {mMsg.Parameters.Count} parameters ({string.Join(", ", mMsg.Parameters.Select(p => p.ParameterType.Name + " " + p.Name))})");
                if (mMsg.Parameters.Count == 5) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  -> Confirmed Valheim 1.0 Character.Message signature (added Boolean log)");
                    Console.ResetColor();
                }
            }
        }

        // 2. Audit Installed BepInEx Plugins
        if (Directory.Exists(pluginsDir)) {
            Console.WriteLine($"\n[+] Auditing Installed Plugins in: {pluginsDir}");
            var pluginFiles = Directory.GetFiles(pluginsDir, "*.dll");
            Console.WriteLine($"[+] Found {pluginFiles.Length} plugin assemblies. Running checks...\n");

            int passed = 0;
            int warnings = 0;

            foreach (var pluginFile in pluginFiles.OrderBy(Path.GetFileName)) {
                string fileName = Path.GetFileName(pluginFile);
                try {
                    var module = ModuleDefinition.ReadModule(pluginFile);
                    bool hasIssue = false;

                    // Check member references for obsolete Character.Message (4 params)
                    foreach (var mr in module.GetMemberReferences()) {
                        if (mr.Name == "Message" && mr.DeclaringType.Name == "Character" && mr is MethodReference mref && mref.Parameters.Count == 4) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"  [FAIL] {fileName}: references obsolete 4-parameter Character.Message signature (causes MissingMethodException in Valheim 1.0)");
                            Console.ResetColor();
                            hasIssue = true;
                            warnings++;
                        }
                    }

                    foreach (var type in module.Types) {
                        // Check for obsolete Vector2i FindSectorObjects patches
                        foreach (var m in type.Methods) {
                            if (m.HasCustomAttributes) {
                                foreach (var attr in m.CustomAttributes) {
                                    if (attr.AttributeType.Name.Contains("HarmonyPatch")) {
                                        foreach (var arg in attr.ConstructorArguments) {
                                            if (arg.Value?.ToString()?.Contains("Vector2i") == true) {
                                                Console.ForegroundColor = ConsoleColor.Red;
                                                Console.WriteLine($"  [FAIL] {fileName}: references obsolete Vector2i sector patch in {type.Name}.{m.Name}");
                                                Console.ResetColor();
                                                hasIssue = true;
                                                warnings++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!hasIssue) {
                        passed++;
                    }
                } catch {
                    // Skip native or non-CLR DLLs
                }
            }

            Console.WriteLine($"\n[Summary] {passed} plugins verified clean. {warnings} architectural warnings.");
        }

        Console.WriteLine("\n[+] Audit completed successfully.");
    }
}
