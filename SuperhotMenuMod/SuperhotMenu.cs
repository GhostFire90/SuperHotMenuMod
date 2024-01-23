using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System;

namespace SuperhotMenuMod
{
    public class MenuEntry
    {
        public enum Entry_Type { Directory, App }
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
            else
            {
                children = new List<MenuEntry>();
                isDir = true;
            }
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
            if (isDir)
            {
                var elem = new XElement(Name);
                foreach(var child in children)
                {
                    elem.Add(child.ToXML());
                }
                return elem;
            }
            else
            {
                var elem = new XElement("item");
                elem.Add(
                        new XAttribute("type", "app"),
                        new XAttribute("name", Name),
                        new XAttribute("appclass", Assembly_Name)
                    );
                return elem;
            }
        }
        private string Name;
        private string Assembly_Name;
        private bool isDir;
        private List<MenuEntry> children;

    }


    [BepInPlugin("superhotMenuModifier", "SHMenu Mod", "0.0")]
    public class SuperhotMenu : BaseUnityPlugin
    {
        static List<XElement> menu_items = new List<XElement>();
        public static void RegisterMenuEntry(MenuEntry entry)
        {
            menu_items.Add(entry.ToXML());
        }
        public static List<XElement> GetMenuItems()
        {
            return menu_items;
        }
        //MethodInfo m_createDirectory;
        
        bool found = false;

        class log_listen : ILogListener
        {
            Harmony harm = new Harmony("SuperHotMenuModifier");
            public void Dispose()
            {
                
            }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if(eventArgs.Level == LogLevel.Message && eventArgs.Data is string && (string)eventArgs.Data == "Chainloader startup complete")
                {
                    harm.PatchAll();   
                }
            }
        }

        void Awake()
        {
            BepInEx.Logging.Logger.Listeners.Add(new log_listen());
            RegisterMenuEntry(new MenuEntry("test", MenuEntry.Entry_Type.App, typeof(APPquit)));
        }
        void Start()
        {
            //m_createDirectory = typeof(piOsMenu).GetMethod("CreateDirectoryStructure", BindingFlags.NonPublic | BindingFlags.Instance);
            
            Logger.Log(BepInEx.Logging.LogLevel.Info, $"Scene num {SceneManager.GetActiveScene().buildIndex}");
        }

        void Update()
        {
            if (!found)
            {
                var objs = GameObject.FindObjectsOfType<piOsMenu>();
                if(objs.Length != 0)
                {
                    found = true;
                    var file = new FileStream("menu_mod.xml", FileMode.OpenOrCreate);
                    objs[0].DATA.Save(file);
                    file.Close();
                    

                    
                }
            }
        }

    }

    class APPTestSHM : SHGUIview
    {
        public override void OnEnter()
        {
            Debug.Log("Worked ?");
            SHGUI.current.PopView();

            base.OnEnter();
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
