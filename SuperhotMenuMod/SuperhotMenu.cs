using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System;
using UnityEngine;

namespace SuperhotMenuMod
{
    public class MenuEntry
    {
        public enum Entry_Type { Directory, App, UserLevel }
        [Obsolete("This is only still here for backwards compat")]
        public MenuEntry(string name, Entry_Type entry, Type app_class)
        {
            if(entry == Entry_Type.App)
            {
                if(app_class != null)
                    Assembly_Name = app_class.AssemblyQualifiedName;
                else
                {
                    throw new Exception("cannot be an app with a null class");
                }
            }
            else if(entry == Entry_Type.Directory)
            {
                children = new List<MenuEntry>();
                isDir = true;
            }
            type = entry;
            Name = name;
        }
        /// <summary>
        /// used to create an "app" entry
        /// </summary>
        /// <param name="name">Visible name of the entry</param>
        /// <param name="app_class">typeof(child class of SHGUIview)</param>
        /// <exception cref="Exception"></exception>
        public MenuEntry(string name, Type app_class)
        {
            type = Entry_Type.App;
            if(app_class == null)
            {
                throw new Exception("cannot be an app with a null class");
            }
            Assembly_Name = app_class.AssemblyQualifiedName;
            isDir = false;
            Name = name;

        }
        /// <summary>
        /// Entry to load a custom asset bundled scene
        /// </summary>
        /// <param name="name">Visible name of the entry</param>
        /// <param name="asset_bundle_name">Path from SH_Data to the asset bundle</param>
        /// <param name="asset_path">Path inside the asset bundle, usually Assets/Scene/SceneName</param>
        public MenuEntry(string name, string asset_bundle_name, string asset_path)
        {
            type = Entry_Type.UserLevel;
            bundle_path = asset_bundle_name;
            scene_path = asset_path;
            isDir = false;
            Name = name;

        }
        /// <summary>
        /// Creates a directory
        /// </summary>
        /// <param name="name">Visible name of the entry</param>
        public MenuEntry(string name)
        {
            type = Entry_Type.Directory;
            children = new List<MenuEntry>();
            isDir = true;
            Name = name;
        }
        public void AddChild(MenuEntry entry)
        {
            if (!isDir)
            {
                throw new Exception("Cannot add child entry to something that is not a directory");
            }
            children.Add(entry);
        }
        public XElement ToXML()
        {
            switch (type)
            {
                case Entry_Type.Directory:
                    {
                        var elem = new XElement(Name);
                        foreach (var child in children)
                        {
                            elem.Add(child.ToXML());
                        }
                        return elem;
                    }
                    
                case Entry_Type.App:
                    {
                        var elem = new XElement("item");
                       
                        elem.Add(
                                new XAttribute("type", "app"),
                                new XAttribute("name", Name),
                                new XAttribute("appclass", Assembly_Name)
                            );
                        return elem;
                    }
                    
                case Entry_Type.UserLevel:
                    {
                        var elem = new XElement("item");
                        elem.Add(
                            new XAttribute("type", "userlevel"),
                            new XAttribute("name", Name),                            
                            new XAttribute("bundle", bundle_path),
                            new XAttribute("scene", scene_path)
                            );
                        return elem;
                    }    
                
            }
            return null;
            
        }
        private string Name;
        private string Assembly_Name;
        private bool isDir;
        private Entry_Type type;
        private List<MenuEntry> children;
        private string bundle_path;
        private string scene_path;

    }


    [BepInPlugin("superhotMenuModifier", "SHMenu Mod", "0.0")]
    public class SuperhotMenu : BaseUnityPlugin
    {
        static Harmony harm = new Harmony("SuperHotMenuModifier");
        static List<XElement> menu_items = new List<XElement>();
        public static void RePatch()
        {
            harm.PatchAll();
            
        }
        //Call in your Awake function in order for the menu to add it
        public static void RegisterMenuEntry(MenuEntry entry)
        {
            menu_items.Add(entry.ToXML());
        }
        public static List<XElement> GetMenuItems()
        {
            return menu_items;
        }
        //MethodInfo m_createDirectory;
        

        class log_listen : ILogListener
        {
            
            public void Dispose()
            {
                
            }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if(eventArgs.Level == LogLevel.Message && eventArgs.Data is string && (string)eventArgs.Data == "Chainloader startup complete")
                {
                    SuperhotMenu.harm.PatchAll();   
                }
            }
        }

        void Awake()
        {
            BepInEx.Logging.Logger.Listeners.Add(new log_listen());
            RegisterMenuEntry(new MenuEntry("testScene", "../test", "Assets/Scenes/SampleScene.unity"));
            //RePatch();

        }
        
        private void Start()
        {
            
        }



    }


    [HarmonyPatch(typeof(piOsMenu), "CreateDirectoryStructure")]
    class MenuTranspiler
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void data_append(piOsMenu self)
        {
            
            foreach (var elem in SuperhotMenuMod.SuperhotMenu.GetMenuItems())
            {
                self.DATA.Element("Data").Add(elem);
            }
            SuperhotMenuMod.SuperhotMenu.GetMenuItems().Clear();
        }

        static FieldInfo f_DATA = typeof(piOsMenu).GetField("DATA", BindingFlags.Instance | BindingFlags.Public);
        static MethodInfo m_data_append = SymbolExtensions.GetMethodInfo((piOsMenu a) => data_append(a));

        [HarmonyTranspiler]
        [HarmonyDebug]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        {
            var codes = new List<CodeInstruction>();
            bool found = false;
            foreach (var instruction in instructions)
            { 
                yield return instruction;
                if (instruction.StoresField(f_DATA))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, m_data_append);
                    //throw new System.Exception("FOUND!");
                }
            }
            if (!found)
            {
                throw new System.Exception("Instruction not found");
            }
            
        }
    }


}
