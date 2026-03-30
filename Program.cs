using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private Mother mother;

        /// <summary>
        /// Program constructor. Here we define our Mother instance, and register 
        /// any extension modules we wish to use in our script.
        /// </summary>
        public Program()
        {
            // Create the Mother instance
            mother = new Mother(this)
            {
                SystemName = "MotherArm",
            };

            // Register Extension Modules
            mother.RegisterModules(new List<IExtensionModule> {
                new ArmModule(mother),
            });
        }

        /// <summary>
        /// Saves the program state. This is called when the program (world) is saved or 
        /// before a recompile. We delegate responsibility for managing save state 
        /// to the LocalStorage core module.
        /// </summary>
        /// <see href="https://github.com/malware-dev/MDK-SE/wiki/The-Anatomy-of-a-Script#the-save-method"/>
        public void Save()
        {
            Storage = mother.Save();
        }

        /// <summary>
        /// The main loop of the program. This is called every tick (update) of 
        /// the the script. We completely delegate activity to Mother.
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="updateType"></param>
        public void Main(string argument, UpdateType updateType)
        {
            mother.Run(argument, updateType);
        }
    }
}
